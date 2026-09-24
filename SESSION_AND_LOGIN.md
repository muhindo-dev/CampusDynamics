# eadmin sessions and sign-in — why people were being logged out, and what was changed

**Date:** 2026-09-24
**Rule being enforced:** a signed-in session lasts **72 hours** and survives a recompile.

---

## The complaint

> "Session keeps expiring every time a change is made on admin side."

That is literally what was happening, and it had nothing to do with timeouts.

---

## What was already configured

```xml
<sessionState mode="InProc" timeout="525600" />        <!-- one year   -->
<forms timeout="1051200" slidingExpiration="true" />   <!-- two years  -->
```

A one-year session and a two-year ticket, and users were still being signed out several times
a day. **The numbers were never the problem.** Anyone tempted to "fix" this by raising a timeout
should stop and read the rest.

---

## The three real causes

### 1. InProc session state, in an application that recompiles constantly

`mode="InProc"` keeps session state inside the AppDomain. This site is served straight out of a
OneDrive-synced folder and uses the Web Site model, so **any** touched file — a sync, an App_Code
edit, a saved page — makes ASP.NET recompile and tear the AppDomain down. Every session in
memory dies with it.

Twenty master pages then do this:

```csharp
if (Session["username"] == null) { /* redirect to login */ }
```

…and bounce the user out, **while their browser is still holding a valid Forms authentication
cookie that nothing was reading**.

### 2. IIS was ending the session every 20 minutes anyway

All three sites (eadmin, eportal, OnlineAdmission) run in the **`.NET v4.5`** application pool,
which carried the IIS defaults:

| setting | was | meaning |
|---|---|---|
| `processModel.idleTimeout` | `00:20:00` + action **Terminate** | 20 minutes with no requests and the worker process is **killed** |
| `recycling.periodicRestart.time` | `1.05:00:00` | forced recycle **every 29 hours**, on a timer |
| `startMode` | `OnDemand` | cold start after every one of those |

A 20-minute idle window cannot coexist with a 72-hour session rule. Go to lunch, come back,
sign in again.

### 3. No fixed machine key

Without `<machineKey>`, ASP.NET generates the validation and decryption keys itself. They are
normally persisted per application, but they are tied to the app-pool identity's profile: if that
profile is not loaded, the identity changes, or the app is moved, the keys are regenerated and
**every authentication cookie in existence becomes undecryptable at once** — which looks exactly
like "the session expired again", for everybody, simultaneously.

---

## What was changed

### A. The ticket became the source of truth — `Global.asax`

```csharp
RestoreSessionFromTicket();   // first thing in Application_PreRequestHandlerExecute
```

If the Forms ticket says who you are and the session has forgotten, **the session is wrong** — so
it is refilled from the ticket and the request carries on. Role and menu access are reloaded from
the database, so a restored session has exactly the rights a fresh sign-in would grant, no more
and no less. It never manufactures an identity: no ticket means genuinely signed out.

A recompile is now invisible instead of a logout.

One deliberate, stated trade: `Session["usernm"]` is **not** restored, because it keys the
single-session guard and its key is built from the password, which is not available at that
point. The concurrent-login rule therefore lapses for a restored session until the next real
sign-in. That is a convenience rule about simultaneous logins, not an authentication control, and
silently signing someone out is the exact fault being fixed.

### B. Honest 72-hour windows — `web.config`

```xml
<sessionState mode="InProc" timeout="4320" cookieless="false" />
<forms loginUrl="~/Default.aspx" name=".CDADMINAUTH" timeout="4320"
       slidingExpiration="true" protection="All" cookieless="UseCookies" path="/" />
```

4320 minutes = 72 hours, sliding: any activity inside a three-day window carries the window
forward. The previous decade-long values were decorative — a ticket that stays valid for two
years is a liability, not a feature.

### C. A fixed machine key — `web.config`

Pinned with a cryptographically generated pair (HMACSHA256 / AES), so the ticket survives
recompiles, recycles and reboots.

> **This signs everyone out once.** Tickets issued under the previous auto-generated key can no
> longer be read. One-time cost, paid at deployment.

### D. IIS stopped ending sessions on a timer

Applied on the server — **not in git**, so it must be reapplied if the box is rebuilt:

```
%windir%\system32\inetsrv\appcmd.exe set apppool ".NET v4.5" /processModel.idleTimeout:00:00:00
%windir%\system32\inetsrv\appcmd.exe set apppool ".NET v4.5" /recycling.periodicRestart.time:00:00:00
%windir%\system32\inetsrv\appcmd.exe set apppool ".NET v4.5" /startMode:AlwaysRunning
```

Cost: the worker process stays resident — 308 MB against 9.9 GB free. This benefits eportal and
OnlineAdmission too, since they share the pool.

---

## A mistake worth recording

`requireSSL="true"` was set on the forms cookie and then **taken back out**, because
`http://eadmin.mru.ac.ug` still answers **200 with no redirect to HTTPS**. With `requireSSL` the
browser refuses to send the sign-in cookie on plain-HTTP requests, so the very problem this work
exists to fix would have become *permanent* for anyone arriving that way.

The right order is: redirect HTTP to HTTPS, confirm nothing depends on the port-80 binding, and
only then turn `requireSSL` on. Worth doing — the sign-in cookie currently travels in clear text
for anyone who reaches the site over HTTP.

---

## What is still true after all this

A recompile still **empties** the session. What changed is that it no longer **signs you out**:
identity is rebuilt from the ticket. Page-level scratch values (a half-filled form keeping state
in `Session["ItemCode"]`, and similar) are still lost on a recycle.

Making those survive too would mean moving session state out of process (`StateServer`, using
the `aspnet_state` service already installed but stopped). That was deliberately **not** done:
out-of-process session serialises the entire session on every request, and this application puts
whole `DataTable`s in there — `Session["AllSupplierLedgersData"]`, the audit export — which would
make every request pay to serialise a ledger. Fixing those call sites first is the prerequisite.

---

## How to verify

1. Sign in to eadmin.
2. Touch any file under `App_Code` (or wait for OneDrive to sync one) to force a recompile.
3. Reload any admin page.

**Before:** bounced to the login screen. **After:** the page loads, still signed in.

Then leave it alone for over 20 minutes and reload — previously a guaranteed sign-out, now it
should simply work.
