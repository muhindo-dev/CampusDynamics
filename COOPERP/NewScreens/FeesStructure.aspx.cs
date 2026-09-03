using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

public partial class COOPERP_NewScreens_FeesStructure : System.Web.UI.Page
{
    private string AcctConnStr
    {
        get
        {
            var cs = ConfigurationManager.ConnectionStrings["accountsConnectionString"];
            return cs != null ? cs.ConnectionString
                : "server=localhost;User Id=root;password=24thdecember1977;database=campus_dynamics_accounts;DefaultCommandTimeout=600;Convert Zero Datetime=True;charset=utf8";
        }
    }

    private string MainConnStr
    {
        get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; }
    }

    // ================================================================
    // LIFECYCLE
    // ================================================================

    /// <summary>Set once an AJAX response has been written, so the page renders nothing after it.</summary>
    private bool _ajaxHandled;

    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);

        // Answered here, before any of the page's own setup runs. CompleteRequest() alone is not
        // enough in WebForms — it skips the remaining pipeline events but the Page still renders,
        // which appended the whole HTML document after the JSON.
        string ajaxAction = Request.QueryString["ajax"];
        if (!string.IsNullOrEmpty(ajaxAction)) { HandleAdjustAjax(ajaxAction); return; }

        // Must populate programme dropdown BEFORE ProcessPostData runs,
        // otherwise the posted selection is lost (ViewState disabled on
        // master page means items are gone, and ProcessPostData's first
        // pass runs before Page_Load).  Load ALL programmes here so the
        // posted value can always be matched.
        PopulatePFProgrammeDropdown("__ALL__");
    }

    // ════════════════════════════════════════════════════════════════════════
    //  WHICH FEE CONSOLE THIS IS
    //
    //  One physical page serves two consoles, chosen by the URL (routes registered in
    //  Global.asax):
    //
    //      COOPERP/NewScreens/FeesStructure.aspx           -> standard (day / weekend)
    //      COOPERP/NewScreens/MainFeesStructure.aspx       -> standard, explicitly
    //      COOPERP/NewScreens/InServiceFeesStructure.aspx  -> in-service
    //
    //  Everything downstream — the listing, the Add button, the modal's default, the
    //  heading — follows this one value, so the in-service console only ever shows and
    //  writes in-service structures. Same file, same table, same code.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The structure this console administers: MAIN or INSERVICE. Taken from the route, with
    /// a ?mode= fallback so the behaviour is still reachable if routing is ever unavailable,
    /// and it is carried across postbacks because a postback to a routed URL keeps the route.
    /// Anything unrecognised means the standard console — the safe default, and what every
    /// existing bookmark of FeesStructure.aspx gets.
    /// </summary>
    protected string PFConsoleSession
    {
        get
        {
            string v = null;
            try
            {
                if (RouteData != null && RouteData.Values.ContainsKey("mode"))
                    v = Convert.ToString(RouteData.Values["mode"]);
            }
            catch { v = null; }

            if (string.IsNullOrEmpty(v)) v = Request.QueryString["mode"];
            if (string.IsNullOrEmpty(v)) v = hfConsoleSession != null ? hfConsoleSession.Value : null;

            v = (v ?? "").Trim().ToUpperInvariant();
            return v == "INSERVICE" ? "INSERVICE" : "MAIN";
        }
    }

    protected bool IsInServiceConsole
    {
        get { return PFConsoleSession == "INSERVICE"; }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        if (_ajaxHandled) return;

        // Persisted so a postback that arrives without route values still knows which
        // console the operator is working in.
        if (hfConsoleSession != null && string.IsNullOrEmpty(hfConsoleSession.Value))
            hfConsoleSession.Value = PFConsoleSession;

        RenderConsoleHeader();

        // On initial load, re-populate to filter out programmes
        // that already have fee structures (cosmetic for the Add modal).
        if (!IsPostBack)
        {
            PopulatePFProgrammeDropdown("");
        }
    }

    /// <summary>
    /// States which console is open and offers the one-click switch to the other. Without
    /// this the two URLs are visually identical, and an operator could enter in-service
    /// rates into the standard structure — repricing every day student on the programme.
    /// </summary>
    private void RenderConsoleHeader()
    {
        if (litConsoleBanner == null) return;

        if (IsInServiceConsole)
        {
            litConsoleBanner.Text =
                "<div class=\"fs-console fs-console--inservice\">"
              + "<span class=\"fs-console__tag\">IN-SERVICE</span>"
              + "<span class=\"fs-console__txt\">You are administering <strong>in-service</strong> fee structures. "
              + "Rates saved here are billed only to students whose study session is in-service; "
              + "the standard structures are untouched.</span>"
              + "<a class=\"fs-console__switch\" href=\"MainFeesStructure.aspx\">Switch to standard &rarr;</a>"
              + "</div>";
            if (Header != null) Header.Title = "In-Service Fee Structure - Campus Dynamics";
        }
        else
        {
            litConsoleBanner.Text =
                "<div class=\"fs-console\">"
              + "<span class=\"fs-console__tag fs-console__tag--main\">STANDARD</span>"
              + "<span class=\"fs-console__txt\">You are administering <strong>standard</strong> (day / weekend) fee "
              + "structures. In-service students are billed these rates until a separate in-service "
              + "structure exists for their programme.</span>"
              + "<a class=\"fs-console__switch\" href=\"InServiceFeesStructure.aspx\">Switch to in-service &rarr;</a>"
              + "</div>";
        }
    }

    // ================================================================
    //  BATCH FEE ADJUSTMENT  (wizard backend)
    // ================================================================
    //  A fee structure decides what every student on that programme is billed, so
    //  this is deliberately preview-then-commit, and the commit acts on the rows the
    //  operator was shown. Three rules protect the figures:
    //
    //    * a cell holding 0 is left alone. Zero means "not charged for this
    //      semester", and adding a block figure to it would invent a fee that the
    //      programme never had.
    //    * a year the programme does not offer (has_year_N = 'No') is never touched.
    //    * a decrease that would take a cell below zero is skipped, not clamped.
    //
    //  Everything that does change is written to fin_fee_adjustment_line first, so
    //  the batch can be reversed exactly.
    // ================================================================

    private static readonly string[] AdjSems = { "1", "2", "3" };

    /// <summary>Emits nothing once an AJAX response has already been written.</summary>
    protected override void Render(HtmlTextWriter writer)
    {
        if (_ajaxHandled) return;
        base.Render(writer);
    }

    private void HandleAdjustAjax(string action)
    {
        _ajaxHandled = true;
        Response.Clear();
        Response.ContentType = "application/json";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        string json;
        try
        {
            switch ((action ?? "").ToLowerInvariant())
            {
                case "adjpreview": json = AdjustPreviewJson(); break;
                case "adjapply":   json = AdjustApplyJson();   break;
                case "adjundo":    json = AdjustUndoJson();    break;
                case "adjhistory": json = AdjustHistoryJson(); break;
                default: json = "{\"success\":false,\"message\":\"Unknown action\"}"; break;
            }
        }
        catch (Exception ex)
        {
            json = "{\"success\":false,\"message\":" + JsStr(ex.Message) + "}";
        }
        Response.Write(json);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private static string JsStr(string s)
    {
        return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(s ?? "");
    }

    /// <summary>The 24 money columns, filtered to the years and semesters asked for.</summary>
    private static List<string> AdjustColumns(string feeType, List<string> years, List<string> sems)
    {
        var cols = new List<string>();
        string suffix = feeType == "TUITION" ? "tuition" : "functional";
        foreach (string y in years)
            foreach (string s in sems)
                cols.Add("y" + y + "_s" + s + "_" + suffix);
        return cols;
    }

    private class AdjustRequest
    {
        public List<int> Ids = new List<int>();
        public string FeeType = "FUNCTIONAL";
        public int Sign = 1;                       // +1 or -1
        public decimal Amount = 0m;
        public List<string> Years = new List<string>();
        public List<string> Sems = new List<string>();
        public string Note = "";
        public string Error = "";
    }

    private AdjustRequest ReadAdjustRequest()
    {
        var q = new AdjustRequest();
        foreach (string part in (Request["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        { int id; if (int.TryParse(part.Trim(), out id) && id > 0 && !q.Ids.Contains(id)) q.Ids.Add(id); }

        q.FeeType = (Request["feeType"] ?? "").Trim().ToUpperInvariant();
        if (q.FeeType != "TUITION" && q.FeeType != "FUNCTIONAL")
        { q.Error = "Choose whether this applies to Tuition or Functional fees."; return q; }

        string dir = (Request["direction"] ?? "").Trim();
        if (dir != "+" && dir != "-")
        { q.Error = "Choose whether this is an increase or a decrease."; return q; }
        q.Sign = dir == "+" ? 1 : -1;

        if (!decimal.TryParse((Request["amount"] ?? "").Trim(), out q.Amount) || q.Amount <= 0)
        { q.Error = "Enter the amount to add or subtract. It must be greater than zero."; return q; }
        if (q.Amount > 100000000m)
        { q.Error = "That amount looks wrong (over 100,000,000). Check it before continuing."; return q; }

        foreach (string y in (Request["years"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        { string t = y.Trim(); if ("1234".Contains(t) && t.Length == 1 && !q.Years.Contains(t)) q.Years.Add(t); }
        foreach (string s in (Request["sems"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        { string t = s.Trim(); if (Array.IndexOf(AdjSems, t) >= 0 && !q.Sems.Contains(t)) q.Sems.Add(t); }

        if (q.Ids.Count == 0) q.Error = "No fee structures were selected.";
        else if (q.Years.Count == 0) q.Error = "Choose at least one year of study.";
        else if (q.Sems.Count == 0) q.Error = "Choose at least one semester.";

        q.Note = (Request["note"] ?? "").Trim();
        q.Years.Sort(); q.Sems.Sort();
        return q;
    }

    /// <summary>
    /// Works out exactly what would change, and changes nothing. Returns one line per
    /// cell with its current and proposed value, plus every cell it would refuse to
    /// touch and why — so the reviewer sees the skips, not just the successes.
    /// </summary>
    private string AdjustPreviewJson()
    {
        AdjustRequest q = ReadAdjustRequest();
        if (q.Error != "") return "{\"success\":false,\"message\":" + JsStr(q.Error) + "}";

        var cols = AdjustColumns(q.FeeType, q.Years, q.Sems);
        var sb = new StringBuilder();
        int changed = 0, skippedZero = 0, skippedNoYear = 0, skippedNegative = 0;
        decimal totalBefore = 0m, totalAfter = 0m, totalDelta = 0m;
        var rows = new List<string>();

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            string sel = "SELECT ID, progcode, has_year_1, has_year_2, has_year_3, has_year_4, " +
                         string.Join(", ", cols.ToArray()) +
                         " FROM fin_programme_fees WHERE ID IN (" + string.Join(",", q.Ids.ConvertAll(i => i.ToString()).ToArray()) + ")";
            using (var cmd = new MySqlCommand(sel, conn))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    int pfId = Convert.ToInt32(rd["ID"]);
                    string prog = Convert.ToString(rd["progcode"]).Trim();
                    var cells = new List<string>();
                    decimal rowBefore = 0m, rowAfter = 0m;

                    foreach (string col in cols)
                    {
                        string yr = col.Substring(1, 1);
                        bool yearOffered = string.Equals(Convert.ToString(rd["has_year_" + yr]).Trim(), "Yes", StringComparison.OrdinalIgnoreCase);
                        decimal cur = rd[col] == DBNull.Value ? 0m : Convert.ToDecimal(rd[col]);
                        string verdict, why;
                        decimal next = cur;

                        if (!yearOffered) { verdict = "SKIP"; why = "Programme has no Year " + yr; skippedNoYear++; }
                        else if (cur <= 0)  { verdict = "SKIP"; why = "Not charged (currently 0)"; skippedZero++; }
                        else
                        {
                            next = cur + (q.Sign * q.Amount);
                            if (next < 0) { verdict = "SKIP"; why = "Would go below zero"; next = cur; skippedNegative++; }
                            else { verdict = "CHANGE"; why = ""; changed++; rowBefore += cur; rowAfter += next; totalBefore += cur; totalAfter += next; }
                        }

                        cells.Add("{\"col\":" + JsStr(col) +
                                  ",\"label\":" + JsStr("Year " + yr + " · Sem " + col.Substring(4, 1)) +
                                  ",\"before\":" + cur.ToString("0.##", CultureInfo.InvariantCulture) +
                                  ",\"after\":" + next.ToString("0.##", CultureInfo.InvariantCulture) +
                                  ",\"verdict\":" + JsStr(verdict) + ",\"why\":" + JsStr(why) + "}");
                    }

                    rows.Add("{\"id\":" + pfId + ",\"progcode\":" + JsStr(prog) +
                             ",\"before\":" + rowBefore.ToString("0.##", CultureInfo.InvariantCulture) +
                             ",\"after\":" + rowAfter.ToString("0.##", CultureInfo.InvariantCulture) +
                             ",\"cells\":[" + string.Join(",", cells.ToArray()) + "]}");
                }
            }
        }
        totalDelta = totalAfter - totalBefore;

        sb.Append("{\"success\":true")
          .Append(",\"structures\":").Append(rows.Count)
          .Append(",\"cellsChanged\":").Append(changed)
          .Append(",\"skippedZero\":").Append(skippedZero)
          .Append(",\"skippedNoYear\":").Append(skippedNoYear)
          .Append(",\"skippedNegative\":").Append(skippedNegative)
          .Append(",\"totalBefore\":").Append(totalBefore.ToString("0.##", CultureInfo.InvariantCulture))
          .Append(",\"totalAfter\":").Append(totalAfter.ToString("0.##", CultureInfo.InvariantCulture))
          .Append(",\"totalDelta\":").Append(totalDelta.ToString("0.##", CultureInfo.InvariantCulture))
          .Append(",\"rows\":[").Append(string.Join(",", rows.ToArray())).Append("]}");
        return sb.ToString();
    }

    /// <summary>
    /// Applies the adjustment. Every rule the preview applied is re-applied here rather
    /// than trusting what the browser sent back, and each changed cell is recorded with
    /// its old value before the UPDATE runs. All of it in one transaction.
    /// </summary>
    private string AdjustApplyJson()
    {
        AdjustRequest q = ReadAdjustRequest();
        if (q.Error != "") return "{\"success\":false,\"message\":" + JsStr(q.Error) + "}";

        string actor = "";
        try { actor = HttpContext.Current.User.Identity.Name; } catch { }
        if (string.IsNullOrEmpty(actor)) actor = "admin";

        var cols = AdjustColumns(q.FeeType, q.Years, q.Sems);
        int changed = 0, skipped = 0, structures = 0;
        long batchId = 0;

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var ins = new MySqlCommand(
                        "INSERT INTO fin_fee_adjustment_batch (performed_by, performed_at, fee_type, direction, amount, years_csv, sems_csv, structures, cells_changed, cells_skipped, note) " +
                        "VALUES (@u, NOW(), @ft, @dir, @amt, @yrs, @sems, 0, 0, 0, @note)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("@u", actor.Length > 100 ? actor.Substring(0, 100) : actor);
                        ins.Parameters.AddWithValue("@ft", q.FeeType);
                        ins.Parameters.AddWithValue("@dir", q.Sign > 0 ? "+" : "-");
                        ins.Parameters.AddWithValue("@amt", q.Amount);
                        ins.Parameters.AddWithValue("@yrs", string.Join(",", q.Years.ToArray()));
                        ins.Parameters.AddWithValue("@sems", string.Join(",", q.Sems.ToArray()));
                        ins.Parameters.AddWithValue("@note", q.Note.Length > 255 ? q.Note.Substring(0, 255) : q.Note);
                        ins.ExecuteNonQuery();
                        batchId = ins.LastInsertedId;
                    }

                    // Read current values inside the transaction, so what is recorded as the
                    // "before" is the value actually being overwritten.
                    var plan = new List<string[]>();   // pfId, progcode, col, old, new
                    string sel = "SELECT ID, progcode, has_year_1, has_year_2, has_year_3, has_year_4, " +
                                 string.Join(", ", cols.ToArray()) +
                                 " FROM fin_programme_fees WHERE ID IN (" + string.Join(",", q.Ids.ConvertAll(i => i.ToString()).ToArray()) + ") FOR UPDATE";
                    using (var cmd = new MySqlCommand(sel, conn, tx))
                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            structures++;
                            int pfId = Convert.ToInt32(rd["ID"]);
                            string prog = Convert.ToString(rd["progcode"]).Trim();
                            foreach (string col in cols)
                            {
                                string yr = col.Substring(1, 1);
                                bool yearOffered = string.Equals(Convert.ToString(rd["has_year_" + yr]).Trim(), "Yes", StringComparison.OrdinalIgnoreCase);
                                decimal cur = rd[col] == DBNull.Value ? 0m : Convert.ToDecimal(rd[col]);
                                if (!yearOffered || cur <= 0) { skipped++; continue; }
                                decimal next = cur + (q.Sign * q.Amount);
                                if (next < 0) { skipped++; continue; }
                                plan.Add(new[] { pfId.ToString(), prog, col,
                                                 cur.ToString("0.##", CultureInfo.InvariantCulture),
                                                 next.ToString("0.##", CultureInfo.InvariantCulture) });
                            }
                        }
                    }

                    foreach (string[] p in plan)
                    {
                        using (var log = new MySqlCommand(
                            "INSERT INTO fin_fee_adjustment_line (batch_id, pf_id, progcode, col_name, old_value, new_value) " +
                            "VALUES (@b,@p,@c,@col,@o,@n)", conn, tx))
                        {
                            log.Parameters.AddWithValue("@b", batchId);
                            log.Parameters.AddWithValue("@p", p[0]);
                            log.Parameters.AddWithValue("@c", p[1]);
                            log.Parameters.AddWithValue("@col", p[2]);
                            log.Parameters.AddWithValue("@o", p[3]);
                            log.Parameters.AddWithValue("@n", p[4]);
                            log.ExecuteNonQuery();
                        }
                        using (var up = new MySqlCommand(
                            "UPDATE fin_programme_fees SET `" + p[2] + "` = @v WHERE ID = @id", conn, tx))
                        {
                            up.Parameters.AddWithValue("@v", p[4]);
                            up.Parameters.AddWithValue("@id", p[0]);
                            changed += up.ExecuteNonQuery();
                        }
                    }

                    using (var fin = new MySqlCommand(
                        "UPDATE fin_fee_adjustment_batch SET structures=@s, cells_changed=@c, cells_skipped=@k WHERE batch_id=@b", conn, tx))
                    {
                        fin.Parameters.AddWithValue("@s", structures);
                        fin.Parameters.AddWithValue("@c", plan.Count);
                        fin.Parameters.AddWithValue("@k", skipped);
                        fin.Parameters.AddWithValue("@b", batchId);
                        fin.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch { try { tx.Rollback(); } catch { } throw; }
            }
        }

        return "{\"success\":true,\"batchId\":" + batchId +
               ",\"structures\":" + structures + ",\"cellsChanged\":" + changed + ",\"cellsSkipped\":" + skipped +
               ",\"message\":" + JsStr(string.Format("{0} fee cell(s) across {1} structure(s) {2} by {3}. Batch #{4} — reversible.",
                    changed, structures, q.Sign > 0 ? "increased" : "reduced",
                    q.Amount.ToString("N0", CultureInfo.InvariantCulture), batchId)) + "}";
    }

    /// <summary>Puts every cell in a batch back to the value it held before that batch ran.</summary>
    private string AdjustUndoJson()
    {
        long batchId;
        if (!long.TryParse((Request["batchId"] ?? "").Trim(), out batchId) || batchId <= 0)
            return "{\"success\":false,\"message\":\"Which batch should be reversed?\"}";

        string actor = "";
        try { actor = HttpContext.Current.User.Identity.Name; } catch { }
        if (string.IsNullOrEmpty(actor)) actor = "admin";

        int restored = 0;
        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var chk = new MySqlCommand("SELECT reverted_at FROM fin_fee_adjustment_batch WHERE batch_id=@b", conn))
            {
                chk.Parameters.AddWithValue("@b", batchId);
                object o = chk.ExecuteScalar();
                if (o == null) return "{\"success\":false,\"message\":\"That batch does not exist.\"}";
                if (o != DBNull.Value) return "{\"success\":false,\"message\":\"That batch has already been reversed.\"}";
            }
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    var lines = new List<string[]>();
                    using (var cmd = new MySqlCommand("SELECT pf_id, col_name, old_value FROM fin_fee_adjustment_line WHERE batch_id=@b", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@b", batchId);
                        using (var rd = cmd.ExecuteReader())
                            while (rd.Read())
                                lines.Add(new[] { Convert.ToString(rd["pf_id"]), Convert.ToString(rd["col_name"]),
                                                  Convert.ToDecimal(rd["old_value"]).ToString("0.##", CultureInfo.InvariantCulture) });
                    }
                    foreach (string[] l in lines)
                    {
                        // The column name comes from our own audit row, but it is still
                        // whitelisted before being concatenated into SQL.
                        if (!System.Text.RegularExpressions.Regex.IsMatch(l[1], @"^y[1-4]_s[1-3]_(tuition|functional)$")) continue;
                        using (var up = new MySqlCommand("UPDATE fin_programme_fees SET `" + l[1] + "` = @v WHERE ID=@id", conn, tx))
                        {
                            up.Parameters.AddWithValue("@v", l[2]);
                            up.Parameters.AddWithValue("@id", l[0]);
                            restored += up.ExecuteNonQuery();
                        }
                    }
                    using (var mark = new MySqlCommand("UPDATE fin_fee_adjustment_batch SET reverted_at=NOW(), reverted_by=@u WHERE batch_id=@b", conn, tx))
                    {
                        mark.Parameters.AddWithValue("@u", actor.Length > 100 ? actor.Substring(0, 100) : actor);
                        mark.Parameters.AddWithValue("@b", batchId);
                        mark.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
                catch { try { tx.Rollback(); } catch { } throw; }
            }
        }
        return "{\"success\":true,\"restored\":" + restored +
               ",\"message\":" + JsStr(restored + " fee cell(s) restored to their previous values. Batch #" + batchId + " reversed.") + "}";
    }

    /// <summary>Recent adjustment batches, so an operator can see and undo what was done.</summary>
    private string AdjustHistoryJson()
    {
        var rows = new List<string>();
        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                "SELECT batch_id, performed_by, performed_at, fee_type, direction, amount, years_csv, sems_csv, " +
                "       structures, cells_changed, reverted_at " +
                "  FROM fin_fee_adjustment_batch ORDER BY batch_id DESC LIMIT 10", conn))
            using (var rd = cmd.ExecuteReader())
                while (rd.Read())
                    rows.Add("{\"batchId\":" + Convert.ToString(rd["batch_id"]) +
                             ",\"by\":" + JsStr(Convert.ToString(rd["performed_by"])) +
                             ",\"at\":" + JsStr(Convert.ToDateTime(rd["performed_at"]).ToString("dd MMM yyyy HH:mm")) +
                             ",\"feeType\":" + JsStr(Convert.ToString(rd["fee_type"])) +
                             ",\"direction\":" + JsStr(Convert.ToString(rd["direction"])) +
                             ",\"amount\":" + Convert.ToDecimal(rd["amount"]).ToString("0.##", CultureInfo.InvariantCulture) +
                             ",\"years\":" + JsStr(Convert.ToString(rd["years_csv"])) +
                             ",\"sems\":" + JsStr(Convert.ToString(rd["sems_csv"])) +
                             ",\"structures\":" + Convert.ToString(rd["structures"]) +
                             ",\"cells\":" + Convert.ToString(rd["cells_changed"]) +
                             ",\"reverted\":" + (rd["reverted_at"] == DBNull.Value ? "false" : "true") + "}");
        }
        return "{\"success\":true,\"batches\":[" + string.Join(",", rows.ToArray()) + "]}";
    }

    protected override void OnPreRender(EventArgs e)
    {
        base.OnPreRender(e);
        LoadProgrammeFees();
        LoadBillingItems();
        LoadBillingSystems();
        LoadBatchBillingBadges();
    }

    // ================================================================
    // PROGRAMME FEE STRUCTURES
    // ================================================================

    private void LoadProgrammeFees()
    {
        string statusFilter = ddlPFStatus.SelectedValue;
        string searchFilter = txtPFSearch.Text.Trim();

        EnsureYear4Columns();
        var sql = new StringBuilder(@"
            SELECT pf.ID, pf.progcode, COALESCE(p.progname,'(Unknown)') AS progname,
                   COALESCE(NULLIF(TRIM(pf.stud_session),''),'MAIN') AS stud_session,
                   COALESCE(f.faculty_name,'') AS faculty_name,
                   pf.has_year_1, pf.has_year_2, pf.has_year_3, pf.has_year_4,
                   pf.y1_s1_tuition, pf.y1_s1_functional,
                   pf.y1_s2_tuition, pf.y1_s2_functional,
                   pf.y1_s3_tuition, pf.y1_s3_functional,
                   pf.y2_s1_tuition, pf.y2_s1_functional,
                   pf.y2_s2_tuition, pf.y2_s2_functional,
                   pf.y2_s3_tuition, pf.y2_s3_functional,
                   pf.y3_s1_tuition, pf.y3_s1_functional,
                   pf.y3_s2_tuition, pf.y3_s2_functional,
                   pf.y3_s3_tuition, pf.y3_s3_functional,
                   pf.y4_s1_tuition, pf.y4_s1_functional,
                   pf.y4_s2_tuition, pf.y4_s2_functional,
                   pf.y4_s3_tuition, pf.y4_s3_functional,
                   pf.is_active
            FROM fin_programme_fees pf
            LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = pf.progcode
            LEFT JOIN campus_dynamics.acad_faculty   f ON f.faculty_code = p.faculty_code
            WHERE 1=1");

        if (!string.IsNullOrEmpty(statusFilter))
            sql.Append(" AND pf.is_active = @status");
        if (!string.IsNullOrEmpty(searchFilter))
            sql.Append(" AND (pf.progcode LIKE @search OR p.progname LIKE @search)");
        // The in-service console lists only in-service structures, and the standard console
        // only standard ones. Mixing them in one grid is what makes the wrong row easy to edit.
        sql.Append(" AND COALESCE(NULLIF(TRIM(pf.stud_session),''),'MAIN') = @consoleSess");
        // Standard structure first, then in-service, so a programme's two rows sit together
        // in a predictable order rather than interleaving with other programmes.
        sql.Append(" ORDER BY pf.is_active DESC, p.progname, pf.progcode, " +
                   "(COALESCE(NULLIF(TRIM(pf.stud_session),''),'MAIN') = 'MAIN') DESC, pf.stud_session");

        var rows = new StringBuilder();
        int count = 0;
        int activeCount = 0;
        int inactiveCount = 0;

        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(sql.ToString(), conn))
            {
                if (!string.IsNullOrEmpty(statusFilter))
                    cmd.Parameters.AddWithValue("@status", statusFilter);
                if (!string.IsNullOrEmpty(searchFilter))
                    cmd.Parameters.AddWithValue("@search", "%" + searchFilter + "%");
                cmd.Parameters.AddWithValue("@consoleSess", PFConsoleSession);

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        count++;
                        int id = Convert.ToInt32(rdr["ID"]);
                        string prog    = rdr["progcode"].ToString().Trim();
                        string pname   = rdr["progname"].ToString();
                        string faculty = rdr["faculty_name"].ToString();
                        string hy1 = rdr["has_year_1"].ToString();
                        string hy2 = rdr["has_year_2"].ToString();
                        string hy3 = rdr["has_year_3"].ToString();
                        string hy4 = rdr["has_year_4"].ToString();
                        string active = rdr["is_active"].ToString();

                        double y1s1t = ToDouble(rdr["y1_s1_tuition"]);
                        double y1s1f = ToDouble(rdr["y1_s1_functional"]);
                        double y1s2t = ToDouble(rdr["y1_s2_tuition"]);
                        double y1s2f = ToDouble(rdr["y1_s2_functional"]);
                        double y1s3t = ToDouble(rdr["y1_s3_tuition"]);
                        double y1s3f = ToDouble(rdr["y1_s3_functional"]);
                        double y2s1t = ToDouble(rdr["y2_s1_tuition"]);
                        double y2s1f = ToDouble(rdr["y2_s1_functional"]);
                        double y2s2t = ToDouble(rdr["y2_s2_tuition"]);
                        double y2s2f = ToDouble(rdr["y2_s2_functional"]);
                        double y2s3t = ToDouble(rdr["y2_s3_tuition"]);
                        double y2s3f = ToDouble(rdr["y2_s3_functional"]);
                        double y3s1t = ToDouble(rdr["y3_s1_tuition"]);
                        double y3s1f = ToDouble(rdr["y3_s1_functional"]);
                        double y3s2t = ToDouble(rdr["y3_s2_tuition"]);
                        double y3s2f = ToDouble(rdr["y3_s2_functional"]);
                        double y3s3t = ToDouble(rdr["y3_s3_tuition"]);
                        double y3s3f = ToDouble(rdr["y3_s3_functional"]);
                        double y4s1t = ToDouble(rdr["y4_s1_tuition"]);
                        double y4s1f = ToDouble(rdr["y4_s1_functional"]);
                        double y4s2t = ToDouble(rdr["y4_s2_tuition"]);
                        double y4s2f = ToDouble(rdr["y4_s2_functional"]);
                        double y4s3t = ToDouble(rdr["y4_s3_tuition"]);
                        double y4s3f = ToDouble(rdr["y4_s3_functional"]);

                        double y1s1total = y1s1t + y1s1f;

                        // Grand total across all applicable years
                        double grandTotal = 0;
                        if (hy1 == "Yes") grandTotal += y1s1t + y1s1f + y1s2t + y1s2f + y1s3t + y1s3f;
                        if (hy2 == "Yes") grandTotal += y2s1t + y2s1f + y2s2t + y2s2f + y2s3t + y2s3f;
                        if (hy3 == "Yes") grandTotal += y3s1t + y3s1f + y3s2t + y3s2f + y3s3t + y3s3f;
                        if (hy4 == "Yes") grandTotal += y4s1t + y4s1f + y4s2t + y4s2f + y4s3t + y4s3f;

                        string statusBadge = active == "Yes"
                            ? "<span class='fs-badge fs-badge--green'>Active</span>"
                            : "<span class='fs-badge fs-badge--red'>Inactive</span>";

                        // Year dots: compact circles showing 1/2/3/4
                        string yrDotsHtml = "<div class='fs-yr-dots'>";
                        yrDotsHtml += string.Format("<span class='fs-yr-dot {0}'>1</span>", hy1 == "Yes" ? "fs-yr-dot--on" : "fs-yr-dot--off");
                        yrDotsHtml += string.Format("<span class='fs-yr-dot {0}'>2</span>", hy2 == "Yes" ? "fs-yr-dot--on" : "fs-yr-dot--off");
                        yrDotsHtml += string.Format("<span class='fs-yr-dot {0}'>3</span>", hy3 == "Yes" ? "fs-yr-dot--on" : "fs-yr-dot--off");
                        yrDotsHtml += string.Format("<span class='fs-yr-dot {0}'>4</span>", hy4 == "Yes" ? "fs-yr-dot--on" : "fs-yr-dot--off");
                        yrDotsHtml += "</div>";

                        if (active == "Yes") activeCount++; else inactiveCount++;

                        // Build the viewPFDetail JS call with all 31 values (incl. year 4)
                        string viewJs = string.Format(
                            "viewPFDetail({0},'{1}','{2}','{3}','{4}','{5}','{6}','{7}',{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31})",
                            id, JsEsc(prog), JsEsc(pname), hy1, hy2, hy3, hy4, active,
                            y1s1t, y1s1f, y1s2t, y1s2f, y1s3t, y1s3f,
                            y2s1t, y2s1f, y2s2t, y2s2f, y2s3t, y2s3f,
                            y3s1t, y3s1f, y3s2t, y3s2f, y3s3t, y3s3f,
                            y4s1t, y4s1f, y4s2t, y4s2f, y4s3t, y4s3f);

                        // Toggle label + icon
                        string toggleLabel = active == "Yes" ? "Deactivate" : "Activate";
                        string toggleIcon = active == "Yes"
                            ? "<svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M18.36 6.64a9 9 0 1 1-12.73 0'></path><line x1='12' y1='2' x2='12' y2='12'></line></svg>"
                            : "<svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='20 6 9 17 4 12'></polyline></svg>";

                        // Build action menu HTML
                        string menuHtml = string.Format(
                            "<div class='fs-action-wrap'>"
                            + "<button type='button' class='fs-action-trigger' onclick='toggleActionMenu(event,this)' title='Actions'>Actions &#9660;</button>"
                            + "<div class='fs-action-menu'>"
                            + "<a class='fs-action-menu__item' href='javascript:void(0)' onclick=\"{0};closeAllMenus();\"><svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z'></path><circle cx='12' cy='12' r='3'></circle></svg>View Details</a>"
                            + "<a class='fs-action-menu__item' href='javascript:void(0)' onclick='editPF({1});closeAllMenus();'><svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7'></path><path d='M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z'></path></svg>Edit Structure</a>"
                            + "<div class='fs-action-menu__divider'></div>"
                            + "<a class='fs-action-menu__item' href='javascript:void(0)' onclick=\"openProcessBilling({1},'{5}','{6}');closeAllMenus();\"><svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><path d='M12 1v22'></path><path d='M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6'></path></svg>Process Billing</a>"
                            + "<div class='fs-action-menu__divider'></div>"
                            + "<a class='fs-action-menu__item' href='javascript:void(0)' onclick='toggleActive({1});closeAllMenus();'>{2}{3}</a>"
                            + "<div class='fs-action-menu__divider'></div>"
                            + "<a class='fs-action-menu__item fs-action-menu__item--danger' href='javascript:void(0)' onclick=\"deleteRow({1},'PF','{4}');closeAllMenus();\"><svg class='fs-action-menu__icon' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2'><polyline points='3 6 5 6 21 6'></polyline><path d='M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'></path></svg>Delete</a>"
                            + "</div></div>",
                            viewJs, id, toggleIcon, toggleLabel,
                            JsEsc(prog + " - " + pname), JsEsc(prog), JsEsc(pname));

                        string facHtml = !string.IsNullOrEmpty(faculty)
                            ? string.Format("<span class='fs-prog-fac'>{0}</span>", Server.HtmlEncode(faculty))
                            : "";

                        // A programme can now hold two structures. Without a badge the two rows
                        // look identical and there is no way to tell which one you are editing.
                        string rowSess = rdr["stud_session"].ToString().Trim().ToUpperInvariant();
                        string sessHtml = rowSess == "INSERVICE"
                            ? "<span class='fs-sess-badge fs-sess-badge--inservice'>IN-SERVICE</span>"
                            : "";

                        rows.AppendFormat(
                            "<tr>"
                            + "<td style='text-align:center;padding:7px 6px;'><input type='checkbox' class='fs-check fs-row-check' data-id='{8}' data-name='{1}' data-total='{9}' onclick='toggleRowCheck(this);' /></td>"
                            + "<td><span class='fs-rownum'>{0}</span></td>"
                            + "<td><div class='fs-prog-cell'><span class='fs-prog-name'>{1}{11}</span><span class='fs-prog-code'>{2}</span>{10}</div></td>"
                            + "<td style='text-align:center'>{3}</td>"
                            + "<td style='text-align:right' class='fs-amount' title='Year 1 Sem 1 Tuition'>{4:N0}</td>"
                            + "<td style='text-align:right' class='fs-amount' style='font-weight:700;color:#05275C;' title='Grand total across all years'>{5:N0}</td>"
                            + "<td style='text-align:center'>{6}</td>"
                            + "<td style='text-align:center'>{7}</td>"
                            + "</tr>",
                            count,
                            Server.HtmlEncode(pname), Server.HtmlEncode(prog),
                            yrDotsHtml,
                            y1s1t, grandTotal,
                            statusBadge, menuHtml,
                            id, grandTotal.ToString("0"), facHtml, sessHtml);
                    }
                }
            }
        }

        litPFRows.Text = rows.ToString();
        litPFCount.Text = string.Format("{0} structures", count);
        litStatActive.Text = activeCount.ToString();
        litStatInactive.Text = inactiveCount.ToString();
        litStatTotal.Text = count.ToString();

        // Count missing: programmes in acad_programme that have no entry in fin_programme_fees
        int missing = 0;
        using (var conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(@"
                SELECT COUNT(DISTINCT p.progcode) AS cnt
                FROM acad_programme p
                INNER JOIN acad_student s ON s.progid = p.progcode
                WHERE p.progcode NOT IN (SELECT progcode FROM campus_dynamics_accounts.fin_programme_fees)", conn))
            {
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    missing = Convert.ToInt32(result);
            }
        }
        litStatMissing.Text = missing.ToString();

        // ── Student overview cards ──────────────────────
        LoadStudentOverviewStats();
        // ── Financial summary cards ─────────────────────
        LoadFinancialSummaryStats();
    }

    // ================================================================
    // STUDENT OVERVIEW STATS (Active / Enrolled / Billed / Unbilled)
    // ================================================================

    private void LoadStudentOverviewStats()
    {
        string acadYear = "";
        try { acadYear = GetCurrentAcadYear(); } catch { }
        litStAcadYear.Text = string.IsNullOrEmpty(acadYear)
            ? "Academic Year: Unknown"
            : "Academic Year: " + Server.HtmlEncode(acadYear);

        if (string.IsNullOrEmpty(acadYear)) return;

        try
        {
            // 1. Active students: acad_student where new_status = 'ACTIVE'
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();

                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM acad_student WHERE new_status = 'ACTIVE'", conn))
                {
                    object r = cmd.ExecuteScalar();
                    litStActive.Text = (r != null && r != DBNull.Value) ? Convert.ToInt64(r).ToString("N0") : "0";
                }

                // Determine which semesters are active for the current academic year
                string semFilter = "0"; // fallback: match nothing
                using (var semCmd = new MySqlCommand(
                    @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                      FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
                {
                    semCmd.Parameters.AddWithValue("@ay", acadYear);
                    using (var rdr = semCmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            var sems = new System.Collections.Generic.List<string>();
                            if (rdr["semester_1_is_active"].ToString() == "Yes") sems.Add("1");
                            if (rdr["semester_2_is_active"].ToString() == "Yes") sems.Add("2");
                            if (rdr["semester_3_is_active"].ToString() == "Yes") sems.Add("3");
                            if (sems.Count > 0) semFilter = string.Join(",", sems.ToArray());
                        }
                    }
                }

                // 2. Enrolled this year: active students registered in active semesters
                string enrolledSql = string.Format(
                    @"SELECT COUNT(DISTINCT r.regno)
                      FROM acad_registration r
                      INNER JOIN acad_student s ON s.regno = r.regno AND s.new_status = 'ACTIVE'
                      WHERE r.acad_year = @ay
                        AND r.semester IN ({0})
                        AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')", semFilter);
                using (var cmd = new MySqlCommand(enrolledSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    object r = cmd.ExecuteScalar();
                    litStEnrolled.Text = (r != null && r != DBNull.Value) ? Convert.ToInt64(r).ToString("N0") : "0";
                }
            }

            // 3 & 4. Billed / Not billed — use accounts DB
            // Re-read active semesters for cross-DB queries
            string activeSemFilter = "0";
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var semCmd = new MySqlCommand(
                    @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                      FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
                {
                    semCmd.Parameters.AddWithValue("@ay", acadYear);
                    using (var rdr = semCmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            var sems = new System.Collections.Generic.List<string>();
                            if (rdr["semester_1_is_active"].ToString() == "Yes") sems.Add("1");
                            if (rdr["semester_2_is_active"].ToString() == "Yes") sems.Add("2");
                            if (rdr["semester_3_is_active"].ToString() == "Yes") sems.Add("3");
                            if (sems.Count > 0) activeSemFilter = string.Join(",", sems.ToArray());
                        }
                    }
                }
            }

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // Billed: active students enrolled (registered) in active semesters AND billed
                string billedSql = string.Format(@"
                    SELECT COUNT(DISTINCT r.regno)
                    FROM campus_dynamics.acad_registration r
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = r.regno AND s.new_status = 'ACTIVE'
                    WHERE r.acad_year = @ay
                      AND r.semester IN ({0})
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                      AND EXISTS(
                          SELECT 1 FROM fin_studentfeestracking ft
                          WHERE ft.regno = r.regno AND ft.acadyear = r.acad_year
                            AND ft.semester = r.semester AND ft.trans_type = 'Bill'
                      )", activeSemFilter);
                using (var cmd = new MySqlCommand(billedSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    object r = cmd.ExecuteScalar();
                    long billed = (r != null && r != DBNull.Value) ? Convert.ToInt64(r) : 0;
                    litStBilled.Text = billed.ToString("N0");
                }

                // Not billed: active students enrolled (registered) in active semesters but no Bill
                // Must include ft.semester = r.semester so a Sem 1 bill doesn't mask an unbilled Sem 2
                string unbilledSql = string.Format(@"
                    SELECT COUNT(DISTINCT r.regno)
                    FROM campus_dynamics.acad_registration r
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = r.regno AND s.new_status = 'ACTIVE'
                    WHERE r.acad_year = @ay
                      AND r.semester IN ({0})
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                      AND NOT EXISTS(
                          SELECT 1 FROM fin_studentfeestracking ft
                          WHERE ft.regno = r.regno AND ft.acadyear = r.acad_year
                            AND ft.semester = r.semester AND ft.trans_type = 'Bill'
                      )", activeSemFilter);
                using (var cmd = new MySqlCommand(unbilledSql, conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    object r = cmd.ExecuteScalar();
                    litStUnbilled.Text = (r != null && r != DBNull.Value) ? Convert.ToInt64(r).ToString("N0") : "0";
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("StudentOverview: " + ex.Message);
        }
    }

    // ================================================================
    // FINANCIAL SUMMARY — Enrolled Students Only
    // ================================================================

    private void LoadFinancialSummaryStats()
    {
        string acadYear = "";
        try { acadYear = GetCurrentAcadYear(); } catch { }
        if (string.IsNullOrEmpty(acadYear)) return;

        try
        {
            // Determine active semesters
            string activeSemFilter = "0";
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                      FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            var sems = new List<string>();
                            if (rdr["semester_1_is_active"].ToString() == "Yes") sems.Add("1");
                            if (rdr["semester_2_is_active"].ToString() == "Yes") sems.Add("2");
                            if (rdr["semester_3_is_active"].ToString() == "Yes") sems.Add("3");
                            if (sems.Count > 0) activeSemFilter = string.Join(",", sems.ToArray());
                        }
                    }
                }
            }

            // All figures scoped to enrolled students:
            //   acad_registration.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
            //   acad_student.new_status = 'ACTIVE'
            //   acad_registration.semester IN (active semesters)
            //   acad_registration.acad_year = current academic year
            //
            // Single efficient query: SUM by item_code bucket + SUM payments
            string sql = string.Format(@"
                SELECT
                    COALESCE(SUM(CASE WHEN ft.trans_type = 'Bill' AND ft.item_code = 1        THEN ft.amount ELSE 0 END), 0) AS tuition_bill,
                    COALESCE(SUM(CASE WHEN ft.trans_type = 'Bill' AND ft.item_code = 52       THEN ft.amount ELSE 0 END), 0) AS functional_bill,
                    COALESCE(SUM(CASE WHEN ft.trans_type = 'Bill' AND ft.item_code NOT IN (1,52) THEN ft.amount ELSE 0 END), 0) AS other_bill,
                    COALESCE(SUM(CASE WHEN ft.trans_type = 'Bill'                             THEN ft.amount ELSE 0 END), 0) AS total_bill,
                    COALESCE(SUM(CASE WHEN ft.trans_type = 'Payment'                          THEN ft.amount ELSE 0 END), 0) AS total_paid
                FROM fin_studentfeestracking ft
                INNER JOIN campus_dynamics.acad_registration r
                    ON r.regno = ft.regno AND r.acad_year = ft.acadyear AND r.semester = ft.semester
                INNER JOIN campus_dynamics.acad_student s
                    ON s.regno = ft.regno AND s.new_status = 'ACTIVE'
                WHERE ft.acadyear = @ay
                  AND ft.semester IN ({0})
                  AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')", activeSemFilter);

            double tuitionBill = 0, functionalBill = 0, totalBill = 0, totalPaid = 0;

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            tuitionBill    = Convert.ToDouble(rdr["tuition_bill"]);
                            functionalBill = Convert.ToDouble(rdr["functional_bill"]);
                            totalBill      = Convert.ToDouble(rdr["total_bill"]);
                            totalPaid      = Convert.ToDouble(rdr["total_paid"]);
                        }
                    }
                }
            }

            double balance = totalBill - totalPaid;
            if (balance < 0) balance = 0;

            litFnTuition.Text    = tuitionBill.ToString("N0");
            litFnFunctional.Text = functionalBill.ToString("N0");
            litFnPaid.Text       = totalPaid.ToString("N0");
            litFnBalance.Text    = balance.ToString("N0");

            // Store total bill for progress bars (JS)
            hfFnTotalBill.Value = totalBill.ToString("F0");

            // Set progress bar widths via startup script
            double pctTuition    = totalBill > 0 ? (tuitionBill / totalBill * 100)    : 0;
            double pctFunctional = totalBill > 0 ? (functionalBill / totalBill * 100) : 0;
            double pctPaid       = totalBill > 0 ? (totalPaid / totalBill * 100)      : 0;
            double pctBalance    = totalBill > 0 ? (balance / totalBill * 100)        : 0;

            // Cap at 100%
            if (pctPaid > 100) pctPaid = 100;
            if (pctBalance > 100) pctBalance = 100;

            string barScript = string.Format(
                "(function(){{" +
                "var bt=document.getElementById('barTuition');if(bt)bt.style.width='{0:F1}%';" +
                "var bf=document.getElementById('barFunctional');if(bf)bf.style.width='{1:F1}%';" +
                "var bp=document.getElementById('barPaid');if(bp)bp.style.width='{2:F1}%';" +
                "var bb=document.getElementById('barBalance');if(bb)bb.style.width='{3:F1}%';" +
                "}})();",
                pctTuition, pctFunctional, pctPaid, pctBalance);

            ScriptManager.RegisterStartupScript(this, GetType(), "fnBars", barScript, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("FinancialSummary: " + ex.Message);
        }
    }

    // ================================================================
    // SAVE PROGRAMME FEE STRUCTURE (Add / Edit)
    // ================================================================

    protected void btnSavePF_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
        string editId = hfEditId.Value;
        string prog = ddlPFProg.SelectedValue;
        string activeVal = ddlPFActive.SelectedValue;
        string hy1 = chkYear1.Checked ? "Yes" : "No";
        string hy2 = chkYear2.Checked ? "Yes" : "No";
        string hy3 = chkYear3.Checked ? "Yes" : "No";
        string hy4 = chkYear4.Checked ? "Yes" : "No";

        if (string.IsNullOrEmpty(prog))
        {
            ShowToast("Please select a programme.", false);
            return;
        }

        // Parse all 24 amount fields (years 1-4)
        double y1s1t = ParseAmt(txtY1S1T.Text);
        double y1s1f = ParseAmt(txtY1S1F.Text);
        double y1s2t = ParseAmt(txtY1S2T.Text);
        double y1s2f = ParseAmt(txtY1S2F.Text);
        double y1s3t = ParseAmt(txtY1S3T.Text);
        double y1s3f = ParseAmt(txtY1S3F.Text);
        double y2s1t = ParseAmt(txtY2S1T.Text);
        double y2s1f = ParseAmt(txtY2S1F.Text);
        double y2s2t = ParseAmt(txtY2S2T.Text);
        double y2s2f = ParseAmt(txtY2S2F.Text);
        double y2s3t = ParseAmt(txtY2S3T.Text);
        double y2s3f = ParseAmt(txtY2S3F.Text);
        double y3s1t = ParseAmt(txtY3S1T.Text);
        double y3s1f = ParseAmt(txtY3S1F.Text);
        double y3s2t = ParseAmt(txtY3S2T.Text);
        double y3s2f = ParseAmt(txtY3S2F.Text);
        double y3s3t = ParseAmt(txtY3S3T.Text);
        double y3s3f = ParseAmt(txtY3S3F.Text);
        double y4s1t = ParseAmt(txtY4S1T.Text);
        double y4s1f = ParseAmt(txtY4S1F.Text);
        double y4s2t = ParseAmt(txtY4S2T.Text);
        double y4s2f = ParseAmt(txtY4S2F.Text);
        double y4s3t = ParseAmt(txtY4S3T.Text);
        double y4s3f = ParseAmt(txtY4S3F.Text);

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // If activating, ensure no other active structure exists for this programme
                // AND THIS SESSION. Scoped by session because a programme legitimately holds
                // two structures now — the standard one and the in-service one. Before this,
                // the check refused any second active row, which made it impossible to add
                // in-service rates at all.
                if (activeVal == "Yes")
                {
                    string checkSql = "SELECT COUNT(*) FROM fin_programme_fees " +
                                      "WHERE progcode=@prog AND is_active='Yes' AND stud_session=@sess";
                    if (!string.IsNullOrEmpty(editId))
                        checkSql += " AND ID <> @editId";

                    using (var chk = new MySqlCommand(checkSql, conn))
                    {
                        chk.Parameters.AddWithValue("@prog", prog);
                        chk.Parameters.AddWithValue("@sess", pfSession);
                        if (!string.IsNullOrEmpty(editId))
                            chk.Parameters.AddWithValue("@editId", Convert.ToInt32(editId));
                        int existing = Convert.ToInt32(chk.ExecuteScalar());
                        if (existing > 0)
                        {
                            ShowToast(string.Format(
                                "This programme already has an active {0} fee structure. Deactivate the existing one first.",
                                PFSessionLabel(pfSession)), false);
                            return;
                        }
                    }
                }

                if (string.IsNullOrEmpty(editId))
                {
                    // INSERT
                    string sql = @"INSERT INTO fin_programme_fees
                        (progcode, stud_session, has_year_1, has_year_2, has_year_3, has_year_4,
                         y1_s1_tuition, y1_s1_functional, y1_s2_tuition, y1_s2_functional, y1_s3_tuition, y1_s3_functional,
                         y2_s1_tuition, y2_s1_functional, y2_s2_tuition, y2_s2_functional, y2_s3_tuition, y2_s3_functional,
                         y3_s1_tuition, y3_s1_functional, y3_s2_tuition, y3_s2_functional, y3_s3_tuition, y3_s3_functional,
                         y4_s1_tuition, y4_s1_functional, y4_s2_tuition, y4_s2_functional, y4_s3_tuition, y4_s3_functional,
                         is_active, created_by)
                        VALUES
                        (@prog, @sess, @hy1, @hy2, @hy3, @hy4,
                         @y1s1t, @y1s1f, @y1s2t, @y1s2f, @y1s3t, @y1s3f,
                         @y2s1t, @y2s1f, @y2s2t, @y2s2f, @y2s3t, @y2s3f,
                         @y3s1t, @y3s1f, @y3s2t, @y3s2f, @y3s3t, @y3s3f,
                         @y4s1t, @y4s1f, @y4s2t, @y4s2f, @y4s3t, @y4s3f,
                         @active, @user)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        AddPFParams(cmd, prog, hy1, hy2, hy3, hy4, activeVal,
                            y1s1t, y1s1f, y1s2t, y1s2f, y1s3t, y1s3f,
                            y2s1t, y2s1f, y2s2t, y2s2f, y2s3t, y2s3f,
                            y3s1t, y3s1f, y3s2t, y3s2f, y3s3t, y3s3f,
                            y4s1t, y4s1f, y4s2t, y4s2f, y4s3t, y4s3f);
                        cmd.Parameters.AddWithValue("@sess", pfSession);
                        cmd.Parameters.AddWithValue("@user", GetCurrentUser());
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast(string.Format("{0} fee structure created for {1}.",
                        PFSessionLabel(pfSession), prog), true);
                }
                else
                {
                    // UPDATE
                    string sql = @"UPDATE fin_programme_fees SET
                        progcode=@prog, has_year_1=@hy1, has_year_2=@hy2, has_year_3=@hy3, has_year_4=@hy4,
                        y1_s1_tuition=@y1s1t, y1_s1_functional=@y1s1f, y1_s2_tuition=@y1s2t, y1_s2_functional=@y1s2f,
                        y1_s3_tuition=@y1s3t, y1_s3_functional=@y1s3f,
                        y2_s1_tuition=@y2s1t, y2_s1_functional=@y2s1f, y2_s2_tuition=@y2s2t, y2_s2_functional=@y2s2f,
                        y2_s3_tuition=@y2s3t, y2_s3_functional=@y2s3f,
                        y3_s1_tuition=@y3s1t, y3_s1_functional=@y3s1f, y3_s2_tuition=@y3s2t, y3_s2_functional=@y3s2f,
                        y3_s3_tuition=@y3s3t, y3_s3_functional=@y3s3f,
                        y4_s1_tuition=@y4s1t, y4_s1_functional=@y4s1f, y4_s2_tuition=@y4s2t, y4_s2_functional=@y4s2f,
                        y4_s3_tuition=@y4s3t, y4_s3_functional=@y4s3f,
                        is_active=@active
                        WHERE ID=@id";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        AddPFParams(cmd, prog, hy1, hy2, hy3, hy4, activeVal,
                            y1s1t, y1s1f, y1s2t, y1s2f, y1s3t, y1s3f,
                            y2s1t, y2s1f, y2s2t, y2s2f, y2s3t, y2s3f,
                            y3s1t, y3s1f, y3s2t, y3s2f, y3s3t, y3s3f,
                            y4s1t, y4s1f, y4s2t, y4s2f, y4s3t, y4s3f);
                        cmd.Parameters.AddWithValue("@id", Convert.ToInt32(editId));
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast(string.Format("Fee structure #{0} updated.", editId), true);
                }
            }
        }
        catch (MySqlException ex)
        {
            if (ex.Number == 1062) // Duplicate key
                ShowToast("A fee structure already exists for this programme. Use Edit instead.", false);
            else
                ShowToast("Database error: " + ex.Message, false);
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + ex.Message, false);
        }
        hfEditId.Value = "";
    }

    private static void AddPFParams(MySqlCommand cmd, string prog,
        string hy1, string hy2, string hy3, string hy4, string active,
        double y1s1t, double y1s1f, double y1s2t, double y1s2f, double y1s3t, double y1s3f,
        double y2s1t, double y2s1f, double y2s2t, double y2s2f, double y2s3t, double y2s3f,
        double y3s1t, double y3s1f, double y3s2t, double y3s2f, double y3s3t, double y3s3f,
        double y4s1t, double y4s1f, double y4s2t, double y4s2f, double y4s3t, double y4s3f)
    {
        cmd.Parameters.AddWithValue("@prog", prog);
        cmd.Parameters.AddWithValue("@hy1", hy1);
        cmd.Parameters.AddWithValue("@hy2", hy2);
        cmd.Parameters.AddWithValue("@hy3", hy3);
        cmd.Parameters.AddWithValue("@hy4", hy4);
        cmd.Parameters.AddWithValue("@active", active);
        cmd.Parameters.AddWithValue("@y1s1t", y1s1t);
        cmd.Parameters.AddWithValue("@y1s1f", y1s1f);
        cmd.Parameters.AddWithValue("@y1s2t", y1s2t);
        cmd.Parameters.AddWithValue("@y1s2f", y1s2f);
        cmd.Parameters.AddWithValue("@y1s3t", y1s3t);
        cmd.Parameters.AddWithValue("@y1s3f", y1s3f);
        cmd.Parameters.AddWithValue("@y2s1t", y2s1t);
        cmd.Parameters.AddWithValue("@y2s1f", y2s1f);
        cmd.Parameters.AddWithValue("@y2s2t", y2s2t);
        cmd.Parameters.AddWithValue("@y2s2f", y2s2f);
        cmd.Parameters.AddWithValue("@y2s3t", y2s3t);
        cmd.Parameters.AddWithValue("@y2s3f", y2s3f);
        cmd.Parameters.AddWithValue("@y3s1t", y3s1t);
        cmd.Parameters.AddWithValue("@y3s1f", y3s1f);
        cmd.Parameters.AddWithValue("@y3s2t", y3s2t);
        cmd.Parameters.AddWithValue("@y3s2f", y3s2f);
        cmd.Parameters.AddWithValue("@y3s3t", y3s3t);
        cmd.Parameters.AddWithValue("@y3s3f", y3s3f);
        cmd.Parameters.AddWithValue("@y4s1t", y4s1t);
        cmd.Parameters.AddWithValue("@y4s1f", y4s1f);
        cmd.Parameters.AddWithValue("@y4s2t", y4s2t);
        cmd.Parameters.AddWithValue("@y4s2f", y4s2f);
        cmd.Parameters.AddWithValue("@y4s3t", y4s3t);
        cmd.Parameters.AddWithValue("@y4s3f", y4s3f);
    }

    // ================================================================
    // DB MIGRATION — ensure year-4 columns exist
    // ================================================================

    private void EnsureYear4Columns()
    {
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                // Check if already migrated
                long exists = 0;
                using (var chk = new MySqlCommand(
                    "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='fin_programme_fees' AND COLUMN_NAME='has_year_4'", conn))
                    exists = (long)chk.ExecuteScalar();
                if (exists > 0) return;

                // Add all year-4 columns in one ALTER
                string alter = @"ALTER TABLE fin_programme_fees
                    ADD COLUMN has_year_4     VARCHAR(3)     NOT NULL DEFAULT 'No'  AFTER has_year_3,
                    ADD COLUMN y4_s1_tuition  DECIMAL(12,2)  NOT NULL DEFAULT 0     AFTER y3_s3_functional,
                    ADD COLUMN y4_s1_functional DECIMAL(12,2) NOT NULL DEFAULT 0    AFTER y4_s1_tuition,
                    ADD COLUMN y4_s2_tuition  DECIMAL(12,2)  NOT NULL DEFAULT 0     AFTER y4_s1_functional,
                    ADD COLUMN y4_s2_functional DECIMAL(12,2) NOT NULL DEFAULT 0    AFTER y4_s2_tuition,
                    ADD COLUMN y4_s3_tuition  DECIMAL(12,2)  NOT NULL DEFAULT 0     AFTER y4_s2_functional,
                    ADD COLUMN y4_s3_functional DECIMAL(12,2) NOT NULL DEFAULT 0    AFTER y4_s3_tuition";
                using (var cmd = new MySqlCommand(alter, conn))
                    cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("EnsureYear4Columns: " + ex.Message);
        }
    }

    // ================================================================
    // TOGGLE ACTIVE / LOAD EDIT
    // ================================================================

    protected void btnToggleActive_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
        string editId = hfEditId.Value;
        string editType = hfEditType.Value;

        if (string.IsNullOrEmpty(editId)) return;
        int pfId = Convert.ToInt32(editId);

        if (editType == "PF_EDIT")
        {
            // Load the record into the modal for editing
            LoadPFForEdit(pfId);
            // Inject script to open the modal
            ScriptManager.RegisterStartupScript(this, GetType(), "openEditPF",
                "openModal('modal-prog-fee');document.getElementById('modalPFTitle').innerText='Edit Fee Structure #" + pfId + "';", true);
            return;
        }

        // PF_TOGGLE - toggle is_active
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                // Get current state
                string currentActive = "";
                string currentProg = "";
                using (var cmd = new MySqlCommand("SELECT progcode, is_active FROM fin_programme_fees WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", pfId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            currentActive = rdr["is_active"].ToString();
                            currentProg = rdr["progcode"].ToString().Trim();
                        }
                    }
                }

                string newActive = currentActive == "Yes" ? "No" : "Yes";

                // If activating, ensure no other active structure for this programme
                if (newActive == "Yes")
                {
                    using (var chk = new MySqlCommand(
                        "SELECT COUNT(*) FROM fin_programme_fees WHERE progcode=@prog AND is_active='Yes' AND ID<>@id", conn))
                    {
                        chk.Parameters.AddWithValue("@prog", currentProg);
                        chk.Parameters.AddWithValue("@id", pfId);
                        int existing = Convert.ToInt32(chk.ExecuteScalar());
                        if (existing > 0)
                        {
                            ShowToast("Cannot activate: another active structure already exists for " + currentProg + ".", false);
                            return;
                        }
                    }
                }

                using (var cmd = new MySqlCommand("UPDATE fin_programme_fees SET is_active=@active WHERE ID=@id", conn))
                {
                    cmd.Parameters.AddWithValue("@active", newActive);
                    cmd.Parameters.AddWithValue("@id", pfId);
                    cmd.ExecuteNonQuery();
                }

                ShowToast(string.Format("Fee structure #{0} ({1}) set to {2}.", pfId, currentProg, newActive == "Yes" ? "Active" : "Inactive"), true);
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error toggling status: " + ex.Message, false);
        }
        hfEditId.Value = "";
    }

    // ════════════════════════════════════════════════════════════════════════
    //  IN-SERVICE FEE STRUCTURE
    //
    //  A programme holds one fee structure per study session. In-service is a delivery
    //  MODE, not a programme — every programme running in-service also runs day and/or
    //  weekend students — so in-service rates cannot be expressed by editing the
    //  programme's single structure without repricing the day cohort too.
    //
    //  Both structures share this one form. The selector picks which is loaded, and the
    //  banner states it, because editing the wrong one silently reprices a whole cohort.
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>The structure currently being edited. Defaults to the programme's standard one.</summary>
    private string pfSession
    {
        get
        {
            string v = (ddlPFSession != null ? ddlPFSession.SelectedValue : "") ?? "";
            v = v.Trim().ToUpperInvariant();
            return v == "INSERVICE" ? "INSERVICE" : "MAIN";
        }
    }

    private static string PFSessionLabel(string session)
    {
        return string.Equals(session, "INSERVICE", StringComparison.OrdinalIgnoreCase)
            ? "in-service" : "standard";
    }

    /// <summary>
    /// Says which structure is on screen. Also warns when an in-service structure does not
    /// exist yet, because until one is saved those students are billed the standard rate —
    /// which is exactly how the historic over-billing happened.
    /// </summary>
    private void RenderPFSessionBanner(string session, string progcode)
    {
        if (litPFSessionBanner == null) return;

        bool inservice = string.Equals(session, "INSERVICE", StringComparison.OrdinalIgnoreCase);
        string prog = string.IsNullOrEmpty(progcode) ? "this programme" : Server.HtmlEncode(progcode);

        if (pfSessionBannerDiv != null)
            pfSessionBannerDiv.Attributes["class"] = "pf-session-banner" + (inservice ? " pf-session-banner--inservice" : "");

        if (inservice)
        {
            litPFSessionBanner.Text =
                "Editing the IN-SERVICE fee structure for " + prog +
                "<span class=\"pf-sb-note\">These rates are billed only to students whose study session is in-service. " +
                "The standard structure is untouched. Every year and semester below is separate from the standard one.</span>";
        }
        else
        {
            litPFSessionBanner.Text =
                "Editing the STANDARD fee structure for " + prog +
                "<span class=\"pf-sb-note\">Billed to day and weekend students. If this programme also runs in-service, " +
                "switch \"Fee structure for\" to In-service and enter those rates separately — otherwise in-service " +
                "students are billed these standard rates.</span>";
        }
    }

    /// <summary>
    /// Switching the selector loads that session's saved structure for the chosen programme,
    /// or clears the amounts when none exists yet so the admin starts from zero rather than
    /// from the other session's figures — which would be an easy way to save a wrong rate.
    /// </summary>
    protected void ddlPFSession_Changed(object sender, EventArgs e)
    {
        string prog = ddlPFProg.SelectedValue;
        if (string.IsNullOrEmpty(prog))
        {
            RenderPFSessionBanner(pfSession, prog);
            return;
        }

        int existingId = 0;
        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    "SELECT ID FROM fin_programme_fees WHERE progcode=@p AND stud_session=@s " +
                    "ORDER BY is_active DESC, ID DESC LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@p", prog);
                    cmd.Parameters.AddWithValue("@s", pfSession);
                    object o = cmd.ExecuteScalar();
                    if (o != null && o != DBNull.Value) existingId = Convert.ToInt32(o);
                }
            }
        }
        catch { existingId = 0; }

        if (existingId > 0)
        {
            // Re-opens the modal and sets the selector and banner itself.
            LoadPFForEdit(existingId);
        }
        else
        {
            // No structure for this session yet: a fresh sheet, not a copy of the other
            // session's figures — inheriting those would make a wrong rate easy to save.
            hfEditId.Value = "";
            ClearPFAmounts();
            SetDdl(ddlPFActive, "Yes");
            RenderPFSessionBanner(pfSession, prog);
            ShowToast(string.Format("No {0} structure exists for {1} yet — enter the rates and save.",
                PFSessionLabel(pfSession), prog), true);
            ShowPFModal();
        }
    }

    /// <summary>
    /// Re-opens the fee-structure modal after a postback. Switching the structure selector
    /// posts back, and without this the dialog would close under the admin mid-edit.
    /// </summary>
    private void ShowPFModal()
    {
        ClientScript.RegisterStartupScript(GetType(), "pfReopen",
            "openModal('modal-prog-fee');", true);
    }

    /// <summary>Zeroes every amount box, leaving the year check-boxes for the admin to set.</summary>
    private void ClearPFAmounts()
    {
        var boxes = new[] {
            txtY1S1T, txtY1S1F, txtY1S2T, txtY1S2F, txtY1S3T, txtY1S3F,
            txtY2S1T, txtY2S1F, txtY2S2T, txtY2S2F, txtY2S3T, txtY2S3F,
            txtY3S1T, txtY3S1F, txtY3S2T, txtY3S2F, txtY3S3T, txtY3S3F,
            txtY4S1T, txtY4S1F, txtY4S2T, txtY4S2F, txtY4S3T, txtY4S3F };
        foreach (var b in boxes) if (b != null) b.Text = "0";
    }

    private void LoadPFForEdit(int pfId)
    {
        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("SELECT * FROM fin_programme_fees WHERE ID=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", pfId);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read()) { ShowToast("Record not found.", false); return; }

                    string prog = rdr["progcode"].ToString().Trim();
                    hfEditId.Value = pfId.ToString();

                    // Repopulate dropdown to include the current programme
                    PopulatePFProgrammeDropdown(prog);
                    SetDdl(ddlPFProg, prog);
                    SetDdl(ddlPFActive, rdr["is_active"].ToString());

                    // Which of the programme's structures this row is, so the banner and the
                    // save path both act on the row actually on screen.
                    string rowSession = rdr["stud_session"] != DBNull.Value
                        ? rdr["stud_session"].ToString().Trim().ToUpperInvariant() : "MAIN";
                    if (rowSession.Length == 0) rowSession = "MAIN";
                    SetDdl(ddlPFSession, rowSession);
                    RenderPFSessionBanner(rowSession, prog);

                    chkYear1.Checked = rdr["has_year_1"].ToString() == "Yes";
                    chkYear2.Checked = rdr["has_year_2"].ToString() == "Yes";
                    chkYear3.Checked = rdr["has_year_3"].ToString() == "Yes";
                    chkYear4.Checked = rdr["has_year_4"].ToString() == "Yes";

                    txtY1S1T.Text = ToDouble(rdr["y1_s1_tuition"]).ToString("0");
                    txtY1S1F.Text = ToDouble(rdr["y1_s1_functional"]).ToString("0");
                    txtY1S2T.Text = ToDouble(rdr["y1_s2_tuition"]).ToString("0");
                    txtY1S2F.Text = ToDouble(rdr["y1_s2_functional"]).ToString("0");
                    txtY1S3T.Text = ToDouble(rdr["y1_s3_tuition"]).ToString("0");
                    txtY1S3F.Text = ToDouble(rdr["y1_s3_functional"]).ToString("0");

                    txtY2S1T.Text = ToDouble(rdr["y2_s1_tuition"]).ToString("0");
                    txtY2S1F.Text = ToDouble(rdr["y2_s1_functional"]).ToString("0");
                    txtY2S2T.Text = ToDouble(rdr["y2_s2_tuition"]).ToString("0");
                    txtY2S2F.Text = ToDouble(rdr["y2_s2_functional"]).ToString("0");
                    txtY2S3T.Text = ToDouble(rdr["y2_s3_tuition"]).ToString("0");
                    txtY2S3F.Text = ToDouble(rdr["y2_s3_functional"]).ToString("0");

                    txtY3S1T.Text = ToDouble(rdr["y3_s1_tuition"]).ToString("0");
                    txtY3S1F.Text = ToDouble(rdr["y3_s1_functional"]).ToString("0");
                    txtY3S2T.Text = ToDouble(rdr["y3_s2_tuition"]).ToString("0");
                    txtY3S2F.Text = ToDouble(rdr["y3_s2_functional"]).ToString("0");
                    txtY3S3T.Text = ToDouble(rdr["y3_s3_tuition"]).ToString("0");
                    txtY3S3F.Text = ToDouble(rdr["y3_s3_functional"]).ToString("0");

                    txtY4S1T.Text = ToDouble(rdr["y4_s1_tuition"]).ToString("0");
                    txtY4S1F.Text = ToDouble(rdr["y4_s1_functional"]).ToString("0");
                    txtY4S2T.Text = ToDouble(rdr["y4_s2_tuition"]).ToString("0");
                    txtY4S2F.Text = ToDouble(rdr["y4_s2_functional"]).ToString("0");
                    txtY4S3T.Text = ToDouble(rdr["y4_s3_tuition"]).ToString("0");
                    txtY4S3F.Text = ToDouble(rdr["y4_s3_functional"]).ToString("0");
                }
            }
        }
    }

    // ================================================================
    // ADD STRUCTURE - prepare modal
    // ================================================================

    protected void btnAddStructure_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
        hfEditId.Value = "";
        PopulatePFProgrammeDropdown("");

        // Clear all amount fields
        txtY1S1T.Text = "0"; txtY1S1F.Text = "0";
        txtY1S2T.Text = "0"; txtY1S2F.Text = "0";
        txtY1S3T.Text = "0"; txtY1S3F.Text = "0";
        txtY2S1T.Text = "0"; txtY2S1F.Text = "0";
        txtY2S2T.Text = "0"; txtY2S2F.Text = "0";
        txtY2S3T.Text = "0"; txtY2S3F.Text = "0";
        txtY3S1T.Text = "0"; txtY3S1F.Text = "0";
        txtY3S2T.Text = "0"; txtY3S2F.Text = "0";
        txtY3S3T.Text = "0"; txtY3S3F.Text = "0";
        txtY4S1T.Text = "0"; txtY4S1F.Text = "0";
        txtY4S2T.Text = "0"; txtY4S2F.Text = "0";
        txtY4S3T.Text = "0"; txtY4S3F.Text = "0";

        chkYear1.Checked = true;
        chkYear2.Checked = false;
        chkYear3.Checked = false;
        chkYear4.Checked = false;
        ddlPFActive.SelectedValue = "No";

        // A new structure defaults to whichever console is open, so "Add" on the in-service
        // console creates an in-service structure without the operator having to remember to
        // change the selector.
        SetDdl(ddlPFSession, PFConsoleSession);
        RenderPFSessionBanner(PFConsoleSession, "");

        ScriptManager.RegisterStartupScript(this, GetType(), "openAddPF",
            "openModal('modal-prog-fee');document.getElementById('modalPFTitle').innerText='Add Programme Fee Structure';", true);
    }

    // ================================================================
    // FILTER HANDLERS
    // ================================================================

    protected void ddlPFStatus_Changed(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
    }

    protected void txtPFSearch_Changed(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
    }

    // ================================================================
    // BATCH OPERATIONS
    // ================================================================

    protected void btnBatchAction_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";
        string action = hfBatchAction.Value;
        string idsStr = hfBatchIds.Value;

        if (string.IsNullOrEmpty(idsStr) || string.IsNullOrEmpty(action))
        {
            ShowToast("No items selected for batch operation.", false);
            return;
        }

        // Parse IDs safely
        string[] parts = idsStr.Split(',');
        var ids = new List<int>();
        foreach (string p in parts)
        {
            int val;
            if (int.TryParse(p.Trim(), out val))
                ids.Add(val);
        }

        if (ids.Count == 0)
        {
            ShowToast("No valid IDs found.", false);
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                if (action == "ACTIVATE" || action == "DEACTIVATE")
                {
                    string newStatus = action == "ACTIVATE" ? "Yes" : "No";
                    int updated = 0;
                    foreach (int id in ids)
                    {
                        using (var cmd = new MySqlCommand(
                            "UPDATE fin_programme_fees SET is_active=@active WHERE ID=@id", conn))
                        {
                            cmd.Parameters.AddWithValue("@active", newStatus);
                            cmd.Parameters.AddWithValue("@id", id);
                            updated += cmd.ExecuteNonQuery();
                        }
                    }
                    string label = action == "ACTIVATE" ? "activated" : "deactivated";
                    ShowToast(string.Format("{0} fee structure(s) {1}.", updated, label), true);
                }
                else if (action == "DELETE")
                {
                    int deleted = 0;
                    foreach (int id in ids)
                    {
                        using (var cmd = new MySqlCommand(
                            "DELETE FROM fin_programme_fees WHERE ID=@id", conn))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            deleted += cmd.ExecuteNonQuery();
                        }
                    }
                    ShowToast(string.Format("{0} fee structure(s) deleted.", deleted), true);
                }
                else if (action == "ADJUST")
                {
                    string pctStr = hfBatchPercent.Value;
                    string feeType = hfBatchFeeType.Value;
                    double pct;
                    if (!double.TryParse(pctStr, out pct) || pct == 0)
                    {
                        ShowToast("Invalid adjustment percentage.", false);
                        return;
                    }

                    double multiplier = 1 + (pct / 100.0);

                    // Build the SET clause based on fee type
                    string setCols = "";
                    if (feeType == "ALL" || feeType == "TUITION")
                    {
                        setCols += "y1_s1_tuition=ROUND(y1_s1_tuition*@mult,0),"
                                 + "y1_s2_tuition=ROUND(y1_s2_tuition*@mult,0),"
                                 + "y1_s3_tuition=ROUND(y1_s3_tuition*@mult,0),"
                                 + "y2_s1_tuition=ROUND(y2_s1_tuition*@mult,0),"
                                 + "y2_s2_tuition=ROUND(y2_s2_tuition*@mult,0),"
                                 + "y2_s3_tuition=ROUND(y2_s3_tuition*@mult,0),"
                                 + "y3_s1_tuition=ROUND(y3_s1_tuition*@mult,0),"
                                 + "y3_s2_tuition=ROUND(y3_s2_tuition*@mult,0),"
                                 + "y3_s3_tuition=ROUND(y3_s3_tuition*@mult,0),";
                    }
                    if (feeType == "ALL" || feeType == "FUNCTIONAL")
                    {
                        setCols += "y1_s1_functional=ROUND(y1_s1_functional*@mult,0),"
                                 + "y1_s2_functional=ROUND(y1_s2_functional*@mult,0),"
                                 + "y1_s3_functional=ROUND(y1_s3_functional*@mult,0),"
                                 + "y2_s1_functional=ROUND(y2_s1_functional*@mult,0),"
                                 + "y2_s2_functional=ROUND(y2_s2_functional*@mult,0),"
                                 + "y2_s3_functional=ROUND(y2_s3_functional*@mult,0),"
                                 + "y3_s1_functional=ROUND(y3_s1_functional*@mult,0),"
                                 + "y3_s2_functional=ROUND(y3_s2_functional*@mult,0),"
                                 + "y3_s3_functional=ROUND(y3_s3_functional*@mult,0),";
                    }

                    // Remove trailing comma
                    if (setCols.EndsWith(","))
                        setCols = setCols.Substring(0, setCols.Length - 1);

                    int adjusted = 0;
                    foreach (int id in ids)
                    {
                        string sql = "UPDATE fin_programme_fees SET " + setCols + " WHERE ID=@id";
                        using (var cmd = new MySqlCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("@mult", multiplier);
                            cmd.Parameters.AddWithValue("@id", id);
                            adjusted += cmd.ExecuteNonQuery();
                        }
                    }

                    string direction = pct > 0 ? "increased" : "decreased";
                    string feeLabel = feeType == "TUITION" ? "tuition" : (feeType == "FUNCTIONAL" ? "functional fees" : "all fees");
                    ShowToast(string.Format("{0} structure(s) {1}: {2} {3} by {4}%.",
                        adjusted, direction, feeLabel, direction, Math.Abs(pct).ToString("0.#")), true);
                }
                else
                {
                    ShowToast("Unknown batch action: " + action, false);
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("Batch error: " + ex.Message, false);
        }

        // Clear batch state
        hfBatchIds.Value = "";
        hfBatchAction.Value = "";
        hfBatchPercent.Value = "";
        hfBatchFeeType.Value = "";
    }

    // ================================================================
    // PROCESS BILLING - Preview & Execute
    // ================================================================

    protected string GetCurrentAcadYear()
    {
        using (var conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("SELECT acadyear FROM acad_acadyears WHERE is_current_year='Yes' LIMIT 1", conn))
            {
                object result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : "";
            }
        }
    }

    protected void btnPreviewBilling_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";

        // --- Validate input ---
        int pfId;
        if (!int.TryParse(hfBillingPfId.Value, out pfId) || pfId <= 0)
        {
            ShowToast("Invalid billing parameters.", false);
            return;
        }

        string acadYear = GetCurrentAcadYear();
        if (string.IsNullOrEmpty(acadYear))
        {
            ShowBillingResult(false, "No Current Academic Year",
                "Please set a current academic year in the system settings before processing billing.");
            return;
        }

        try
        {
            // --- 1) Load fee structure in one query ---
            string progcode = "";
            string progname = "";
            bool pfActive = false;
            var feeLookup = new Dictionary<string, double[]>();

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT pf.*, COALESCE(p.progname,'(Unknown)') AS progname
                      FROM fin_programme_fees pf
                      LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = pf.progcode
                      WHERE pf.ID=@id", conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@id", pfId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            ShowBillingResult(false, "Fee Structure Not Found",
                                "The fee structure record (ID=" + pfId + ") was not found.");
                            return;
                        }
                        progcode = rdr["progcode"].ToString().Trim();
                        progname = rdr["progname"].ToString().Trim();
                        pfActive = rdr["is_active"].ToString() == "Yes";
                        for (int yr = 1; yr <= 3; yr++)
                        {
                            if (rdr[string.Format("has_year_{0}", yr)].ToString() != "Yes") continue;
                            for (int sem = 1; sem <= 3; sem++)
                            {
                                double t = ToDouble(rdr[string.Format("y{0}_s{1}_tuition", yr, sem)]);
                                double f = ToDouble(rdr[string.Format("y{0}_s{1}_functional", yr, sem)]);
                                feeLookup[string.Format("{0}_{1}", yr, sem)] = new double[] { t, f };
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(progcode))
            {
                ShowBillingResult(false, "Invalid Fee Structure", "The fee structure has no programme code.");
                return;
            }

            // --- 2) Load ALL registered students in one query ---
            var allStudents = new List<StudentBillingInfo>();
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT r.regno, r.studyyear, r.semester, r.regstatus,
                           COALESCE(s.firstname,'') AS firstname,
                           COALESCE(s.othername,'') AS othername,
                           COALESCE(s.studsesion,'') AS studsesion
                    FROM acad_registration r
                    INNER JOIN acad_student s ON s.regno = r.regno
                    WHERE s.progid = @prog
                      AND r.acad_year = @acad
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                    ORDER BY r.semester, s.firstname, s.othername", conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@prog", progcode);
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int studyYr = Convert.ToInt32(rdr["studyyear"]);
                            int regSem = Convert.ToInt32(rdr["semester"]);
                            string key = string.Format("{0}_{1}", studyYr, regSem);
                            double tuit = 0, func = 0;
                            if (feeLookup.ContainsKey(key))
                            {
                                tuit = feeLookup[key][0];
                                func = feeLookup[key][1];
                            }

                            allStudents.Add(new StudentBillingInfo
                            {
                                RegNo = rdr["regno"].ToString().Trim(),
                                FullName = string.Format("{0} {1}",
                                    rdr["firstname"].ToString().Trim(),
                                    rdr["othername"].ToString().Trim()).Trim(),
                                StudyYear = studyYr,
                                Semester = regSem,
                                Session = rdr["studsesion"].ToString().Trim(),
                                RegStatus = rdr["regstatus"].ToString().Trim(),
                                Tuition = tuit,
                                Functional = func
                            });
                        }
                    }
                }
            }

            if (allStudents.Count == 0)
            {
                // No registrations found — still show preview with empty panels
                BuildBillingPreviewHtml(
                    new List<StudentBillingInfo>(),
                    new List<StudentBillingInfo>(),
                    progcode, progname, acadYear, pfId, pfActive, 0);
                return;
            }

            // --- 3) Batch-load ALL existing bills in ONE query (replaces N+1 queries) ---
            // Key = "regno|semester|itemcode" → amount
            var existingBills = new Dictionary<string, double>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT ft.regno, ft.semester, ft.item_code, SUM(ft.amount) AS total_billed
                    FROM fin_studentfeestracking ft
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = ft.regno
                    WHERE s.progid = @prog
                      AND ft.acadyear = @acad
                      AND ft.trans_type = 'Bill'
                      AND ft.item_code IN (1, 52)
                    GROUP BY ft.regno, ft.semester, ft.item_code", conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@prog", progcode);
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string bKey = string.Format("{0}|{1}|{2}",
                                rdr["regno"].ToString().Trim(),
                                Convert.ToInt32(rdr["semester"]),
                                Convert.ToInt32(rdr["item_code"]));
                            existingBills[bKey] = Convert.ToDouble(rdr["total_billed"]);
                        }
                    }
                }
            }

            // --- 4) Classify students using in-memory lookups (zero DB calls) ---
            var unbilledStudents = new List<StudentBillingInfo>();
            var billedStudents = new List<StudentBillingInfo>();
            int noFeeCount = 0;

            foreach (var stu in allStudents)
            {
                string tKey = string.Format("{0}|{1}|1", stu.RegNo, stu.Semester);
                string fKey = string.Format("{0}|{1}|52", stu.RegNo, stu.Semester);
                bool hasTuitionBill = existingBills.ContainsKey(tKey);
                bool hasFunctionalBill = existingBills.ContainsKey(fKey);

                if (hasTuitionBill && hasFunctionalBill)
                {
                    // Fully billed — show actual billed amounts on the "Already Billed" side
                    stu.Tuition = existingBills[tKey];
                    stu.Functional = existingBills[fKey];
                    billedStudents.Add(stu);
                }
                else if (hasTuitionBill || hasFunctionalBill)
                {
                    // Partially billed — unbilled portion goes to "To Be Billed"
                    double remainingTuit = hasTuitionBill ? 0 : stu.Tuition;
                    double remainingFunc = hasFunctionalBill ? 0 : stu.Functional;
                    if (remainingTuit > 0 || remainingFunc > 0)
                    {
                        stu.Tuition = remainingTuit;
                        stu.Functional = remainingFunc;
                        unbilledStudents.Add(stu);
                    }
                    else
                    {
                        // The billed items covered everything, treat as billed
                        stu.Tuition = hasTuitionBill ? existingBills[tKey] : 0;
                        stu.Functional = hasFunctionalBill ? existingBills[fKey] : 0;
                        billedStudents.Add(stu);
                    }
                }
                else
                {
                    // Not billed at all
                    if (stu.Tuition > 0 || stu.Functional > 0)
                    {
                        unbilledStudents.Add(stu);
                    }
                    else
                    {
                        noFeeCount++; // registered but no fee structure applies to their year/sem
                    }
                }
            }

            // --- 5) Build HTML output ---
            BuildBillingPreviewHtml(unbilledStudents, billedStudents, progcode, progname,
                acadYear, pfId, pfActive, noFeeCount);
        }
        catch (Exception ex)
        {
            ShowBillingResult(false, "Preview Error",
                "An error occurred while generating the billing preview: " + ex.Message);
        }
    }

    private void BuildBillingPreviewHtml(
        List<StudentBillingInfo> unbilled,
        List<StudentBillingInfo> billed,
        string progcode, string progname, string acadYear, int pfId,
        bool pfActive, int noFeeCount)
    {
        double totalUnbilledTuition = 0, totalUnbilledFunc = 0;
        double totalBilledTuition = 0, totalBilledFunc = 0;

        // Unbilled students table
        var ubHtml = new StringBuilder();
        if (unbilled.Count == 0)
        {
            ubHtml.Append("<div class='pb-empty'><svg width='36' height='36' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.5'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'></path><polyline points='22 4 12 14.01 9 11.01'></polyline></svg>All registered students have been billed.</div>");
        }
        else
        {
            ubHtml.Append("<table class='pb-table'><thead><tr><th>#</th><th>Student</th><th>Yr</th><th>Sem</th><th>Session</th><th style='text-align:right'>Tuition</th><th style='text-align:right'>Functional</th><th style='text-align:right'>Total</th></tr></thead><tbody>");
            for (int i = 0; i < unbilled.Count; i++)
            {
                var s = unbilled[i];
                double total = s.Tuition + s.Functional;
                totalUnbilledTuition += s.Tuition;
                totalUnbilledFunc += s.Functional;
                ubHtml.AppendFormat(
                    "<tr><td>{0}</td><td><span class='pb-stud-name'>{1}</span><span class='pb-stud-regno'>{2}</span></td>"
                    + "<td>Yr {3}</td><td>S{8}</td><td>{4}</td>"
                    + "<td class='pb-amt pb-amt--tuition'>{5:N0}</td>"
                    + "<td class='pb-amt pb-amt--func'>{6:N0}</td>"
                    + "<td class='pb-amt pb-amt--total'>{7:N0}</td></tr>",
                    i + 1, Server.HtmlEncode(s.FullName), Server.HtmlEncode(s.RegNo),
                    s.StudyYear, Server.HtmlEncode(s.Session),
                    s.Tuition, s.Functional, total, s.Semester);
            }
            ubHtml.Append("</tbody></table>");
        }
        litUnbilledStudents.Text = ubHtml.ToString();

        // Billed students table
        var bHtml = new StringBuilder();
        if (billed.Count == 0)
        {
            bHtml.Append("<div class='pb-empty'><svg width='36' height='36' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='1.5'><circle cx='12' cy='12' r='10'></circle><line x1='12' y1='8' x2='12' y2='16'></line><line x1='8' y1='12' x2='16' y2='12'></line></svg>No students have been billed yet for this academic year.</div>");
        }
        else
        {
            bHtml.Append("<table class='pb-table'><thead><tr><th>#</th><th>Student</th><th>Yr</th><th>Sem</th><th style='text-align:right'>Tuition</th><th style='text-align:right'>Functional</th><th style='text-align:right'>Total</th></tr></thead><tbody>");
            for (int i = 0; i < billed.Count; i++)
            {
                var s = billed[i];
                double total = s.Tuition + s.Functional;
                totalBilledTuition += s.Tuition;
                totalBilledFunc += s.Functional;
                bHtml.AppendFormat(
                    "<tr><td>{0}</td><td><span class='pb-stud-name'>{1}</span><span class='pb-stud-regno'>{2}</span></td>"
                    + "<td>Yr {3}</td><td>S{7}</td>"
                    + "<td class='pb-amt pb-amt--tuition'>{4:N0}</td>"
                    + "<td class='pb-amt pb-amt--func'>{5:N0}</td>"
                    + "<td class='pb-amt pb-amt--total'>{6:N0}</td></tr>",
                    i + 1, Server.HtmlEncode(s.FullName), Server.HtmlEncode(s.RegNo),
                    s.StudyYear, s.Tuition, s.Functional, total, s.Semester);
            }
            bHtml.Append("</tbody></table>");
        }
        litBilledStudents.Text = bHtml.ToString();

        double totalUnbilled = totalUnbilledTuition + totalUnbilledFunc;
        double totalBilled = totalBilledTuition + totalBilledFunc;
        int totalRegistered = unbilled.Count + billed.Count + noFeeCount;

        // Summary cards
        var sumHtml = new StringBuilder();
        sumHtml.AppendFormat(
            "<div class='pb-summary-card'><div class='pb-summary-card__val pb-summary-card__val--blue'>{0}</div><div class='pb-summary-card__lbl'>To Be Billed</div></div>", unbilled.Count);
        sumHtml.AppendFormat(
            "<div class='pb-summary-card'><div class='pb-summary-card__val pb-summary-card__val--green'>{0}</div><div class='pb-summary-card__lbl'>Already Billed</div></div>", billed.Count);
        sumHtml.AppendFormat(
            "<div class='pb-summary-card'><div class='pb-summary-card__val pb-summary-card__val--amber'>{0}</div><div class='pb-summary-card__lbl'>Total Registered</div></div>", totalRegistered);
        sumHtml.AppendFormat(
            "<div class='pb-summary-card'><div class='pb-summary-card__val pb-summary-card__val--purple'>{0:N0}</div><div class='pb-summary-card__lbl'>Amount to Bill</div></div>", totalUnbilled);

        // Warning cards: inactive fee structure or students with no applicable fees
        if (!pfActive)
        {
            sumHtml.Append("<div class='pb-summary-card' style='border-color:#ef5350;background:#fff5f5;'>"
                + "<div class='pb-summary-card__val' style='color:#c62828;font-size:14px;'>INACTIVE</div>"
                + "<div class='pb-summary-card__lbl' style='color:#c62828;'>Fee Structure Inactive</div></div>");
        }
        if (noFeeCount > 0)
        {
            sumHtml.AppendFormat(
                "<div class='pb-summary-card' style='border-color:#ff9800;background:#fff8e1;'>"
                + "<div class='pb-summary-card__val' style='color:#e65100;'>{0}</div>"
                + "<div class='pb-summary-card__lbl' style='color:#e65100;'>No Fee Defined</div></div>", noFeeCount);
        }
        litBillingSummary.Text = sumHtml.ToString();

        // Determine whether proceed button should be enabled:
        // Disabled if no students to bill OR fee structure is inactive
        bool disableProceed = unbilled.Count == 0 || !pfActive;

        // Inject JS to show preview and restore state after postback
        string showScript = string.Format(
            @"_pbPfId={5};_pbProgCode='{6}';_pbProgName='{7}';
              document.getElementById('pbProgName').innerText='{7}';
              document.getElementById('pbProgCode').innerText='{6}';
              document.getElementById('modalProcessBillingTitle').innerText='Process Billing - {6}';
              document.getElementById('pbAcadYear').innerText='{8}';
              document.getElementById('pbLoading').className='pb-progress';
              document.getElementById('pbPlaceholder').style.display='none';
              document.getElementById('pbPreviewContent').style.display='';
              document.getElementById('pbUnbilledCount').innerText='{0}';
              document.getElementById('pbBilledCount').innerText='{1}';
              document.getElementById('pbUnbilledTotal').innerText='{2}';
              document.getElementById('pbBilledTotal').innerText='{3}';
              document.getElementById('btnPBPreview').disabled=false;
              _pbPreviewDone=true;
              document.getElementById('btnPBProceed').disabled={4};
              openModal('modal-process-billing');",
            unbilled.Count, billed.Count,
            totalUnbilled.ToString("N0"), totalBilled.ToString("N0"),
            disableProceed ? "true" : "false",
            pfId, JsEsc(progcode), JsEsc(progname), JsEsc(acadYear));

        ScriptManager.RegisterStartupScript(this, GetType(), "showBillingPreview", showScript, true);
    }

    protected void btnExecuteBilling_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";

        // --- Validate input ---
        int pfId;
        if (!int.TryParse(hfBillingPfId.Value, out pfId) || pfId <= 0)
        {
            ShowBillingResult(false, "Invalid Parameters", "Missing or invalid fee structure ID.");
            return;
        }

        string acadYear = GetCurrentAcadYear();
        string currentUser = GetCurrentUser();

        if (string.IsNullOrEmpty(acadYear))
        {
            ShowBillingResult(false, "No Current Academic Year", "Please set a current academic year first.");
            return;
        }
        if (string.IsNullOrEmpty(currentUser)) currentUser = "SYSTEM";

        try
        {
            // --- 1) Load fee structure (must be ACTIVE) ---
            string progcode = "";
            var feeLookup = new Dictionary<string, double[]>();

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT * FROM fin_programme_fees WHERE ID=@id AND is_active='Yes'", conn))
                {
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@id", pfId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            ShowBillingResult(false, "Fee Structure Inactive",
                                "Only active fee structures can be used for billing. "
                                + "Please activate the fee structure first.");
                            return;
                        }
                        progcode = rdr["progcode"].ToString().Trim();
                        for (int yr = 1; yr <= 3; yr++)
                        {
                            if (rdr[string.Format("has_year_{0}", yr)].ToString() != "Yes") continue;
                            for (int sem = 1; sem <= 3; sem++)
                            {
                                double t = ToDouble(rdr[string.Format("y{0}_s{1}_tuition", yr, sem)]);
                                double f = ToDouble(rdr[string.Format("y{0}_s{1}_functional", yr, sem)]);
                                feeLookup[string.Format("{0}_{1}", yr, sem)] = new double[] { t, f };
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(progcode))
            {
                ShowBillingResult(false, "Invalid Fee Structure", "The fee structure has no programme code.");
                return;
            }

            // --- 2) Load registered students ---
            var students = new List<StudentBillingInfo>();
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT r.regno, r.studyyear, r.semester,
                           COALESCE(s.studsesion,'') AS studsesion
                    FROM acad_registration r
                    INNER JOIN acad_student s ON s.regno = r.regno
                    WHERE s.progid = @prog
                      AND r.acad_year = @acad
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                    ORDER BY r.semester, s.firstname", conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@prog", progcode);
                    cmd.Parameters.AddWithValue("@acad", acadYear);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int regSem = Convert.ToInt32(rdr["semester"]);
                            string key = string.Format("{0}_{1}", Convert.ToInt32(rdr["studyyear"]), regSem);
                            double tuit = 0, func = 0;
                            if (feeLookup.ContainsKey(key))
                            {
                                tuit = feeLookup[key][0];
                                func = feeLookup[key][1];
                            }

                            students.Add(new StudentBillingInfo
                            {
                                RegNo = rdr["regno"].ToString().Trim(),
                                StudyYear = Convert.ToInt32(rdr["studyyear"]),
                                Semester = regSem,
                                Session = rdr["studsesion"].ToString().Trim(),
                                Tuition = tuit,
                                Functional = func
                            });
                        }
                    }
                }
            }

            if (students.Count == 0)
            {
                ShowBillingResult(false, "No Students Found",
                    "No registered students found for this programme in the current academic year.");
                return;
            }

            // --- 3) Batch-load existing bills to pre-filter already-billed students ---
            var existingBills = new HashSet<string>(); // key = "regno|semester|itemcode"
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(@"
                    SELECT ft.regno, ft.semester, ft.item_code
                    FROM fin_studentfeestracking ft
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = ft.regno
                    WHERE s.progid = @prog
                      AND ft.acadyear = @acad
                      AND ft.trans_type = 'Bill'
                      AND ft.item_code IN (1, 52)
                    GROUP BY ft.regno, ft.semester, ft.item_code", conn))
                {
                    cmd.CommandTimeout = 60;
                    cmd.Parameters.AddWithValue("@prog", progcode);
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string bKey = string.Format("{0}|{1}|{2}",
                                rdr["regno"].ToString().Trim(),
                                Convert.ToInt32(rdr["semester"]),
                                Convert.ToInt32(rdr["item_code"]));
                            existingBills.Add(bKey);
                        }
                    }
                }
            }

            // --- 4) Process billing — only for genuinely unbilled students ---
            int billedCount = 0;
            int alreadyBilledCount = 0;
            int noFeeSkipped = 0;
            int errorCount = 0;
            double totalBilledAmount = 0;
            var errors = new List<string>();

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                foreach (var stu in students)
                {
                    // Skip students with no applicable fees (year/sem not in fee structure)
                    if (stu.Tuition <= 0 && stu.Functional <= 0)
                    {
                        noFeeSkipped++;
                        continue;
                    }

                    // Check if BOTH tuition and functional already billed via in-memory lookup
                    string tKey = string.Format("{0}|{1}|1", stu.RegNo, stu.Semester);
                    string fKey = string.Format("{0}|{1}|52", stu.RegNo, stu.Semester);
                    bool tuitBilled = existingBills.Contains(tKey);
                    bool funcBilled = existingBills.Contains(fKey);

                    if (tuitBilled && funcBilled)
                    {
                        alreadyBilledCount++;
                        continue;
                    }

                    try
                    {
                        // Call fin_BillProgrammeFees SP which internally calls fin_TermlyItemBillingFN
                        // The SP function has built-in duplicate check as a safety net
                        using (var cmd = new MySqlCommand(
                            "CALL fin_BillProgrammeFees(@reg, @prog, @sess, @yr, @sem, @acad, @user, @csid)", conn))
                        {
                            cmd.CommandTimeout = 30;
                            cmd.Parameters.AddWithValue("@reg", stu.RegNo);
                            cmd.Parameters.AddWithValue("@prog", progcode);
                            cmd.Parameters.AddWithValue("@sess", string.IsNullOrEmpty(stu.Session) ? "Day" : stu.Session);
                            cmd.Parameters.AddWithValue("@yr", stu.StudyYear);
                            cmd.Parameters.AddWithValue("@sem", stu.Semester);
                            cmd.Parameters.AddWithValue("@acad", acadYear);
                            cmd.Parameters.AddWithValue("@user", currentUser);
                            cmd.Parameters.AddWithValue("@csid", "BATCH");
                            cmd.ExecuteNonQuery();
                        }

                        billedCount++;
                        // Only count fees that were not previously billed
                        if (!tuitBilled) totalBilledAmount += stu.Tuition;
                        if (!funcBilled) totalBilledAmount += stu.Functional;

                        // Mark as billed in memory to prevent re-processing
                        existingBills.Add(tKey);
                        existingBills.Add(fKey);
                    }
                    catch (Exception ex)
                    {
                        // MySQL error 1062 = duplicate key — means the DB-level
                        // uniqueness constraint caught a race condition. This is
                        // expected and safe; count as already-billed, not error.
                        var mex = ex as MySqlException;
                        if (mex != null && mex.Number == 1062)
                        {
                            alreadyBilledCount++;
                            existingBills.Add(tKey);
                            existingBills.Add(fKey);
                        }
                        else
                        {
                            errorCount++;
                            if (errors.Count < 5)
                                errors.Add(string.Format("{0} (Yr{1}/S{2}): {3}",
                                    stu.RegNo, stu.StudyYear, stu.Semester, ex.Message));
                        }
                    }
                }
            }

            // --- 5) Build result message ---
            if (errorCount == 0 && billedCount > 0)
            {
                string detail = string.Format(
                    "{0} student(s) billed successfully. Total amount billed: {1:N0}.",
                    billedCount, totalBilledAmount);
                if (alreadyBilledCount > 0)
                    detail += string.Format(" {0} already billed (skipped).", alreadyBilledCount);
                if (noFeeSkipped > 0)
                    detail += string.Format(" {0} skipped (no fee structure for their year/semester).", noFeeSkipped);

                ShowBillingResult(true,
                    string.Format("Billing Complete \u2014 {0} Student(s) Processed", billedCount),
                    detail);
            }
            else if (errorCount == 0 && billedCount == 0)
            {
                string detail = "No new billing was required.";
                if (alreadyBilledCount > 0)
                    detail += string.Format(" {0} student(s) were already billed.", alreadyBilledCount);
                if (noFeeSkipped > 0)
                    detail += string.Format(" {0} skipped (no fee structure applies).", noFeeSkipped);

                ShowBillingResult(true, "No Billing Needed", detail);
            }
            else
            {
                string errDetail = string.Format(
                    "{0} billed successfully, {1} error(s), {2} already billed, {3} no-fee skipped.",
                    billedCount, errorCount, alreadyBilledCount, noFeeSkipped);
                if (errors.Count > 0)
                    errDetail += " Errors: " + string.Join("; ", errors.ToArray());

                ShowBillingResult(false, "Billing Completed with Errors", errDetail);
            }
        }
        catch (Exception ex)
        {
            ShowBillingResult(false, "Billing Error",
                "An unexpected error occurred during billing execution: " + ex.Message);
        }
    }

    private void ShowBillingResult(bool success, string title, string detail)
    {
        string icon = success
            ? "<svg class='pb-result__icon' viewBox='0 0 24 24' fill='none' stroke='#2e7d32' stroke-width='2'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'></path><polyline points='22 4 12 14.01 9 11.01'></polyline></svg>"
            : "<svg class='pb-result__icon' viewBox='0 0 24 24' fill='none' stroke='#c62828' stroke-width='2'><circle cx='12' cy='12' r='10'></circle><line x1='15' y1='9' x2='9' y2='15'></line><line x1='9' y1='9' x2='15' y2='15'></line></svg>";

        string titleClass = success ? "pb-result__title pb-result__title--success" : "pb-result__title pb-result__title--error";

        litBillingResult.Text = string.Format(
            "{0}<div class='{1}'>{2}</div><div class='pb-result__detail'>{3}</div>"
            + "<div style='margin-top:16px;'><button type='button' class='fs-btn fs-btn--primary' onclick='closeProcessBilling();'>Close</button></div>",
            icon, titleClass, Server.HtmlEncode(title), Server.HtmlEncode(detail));

        string showScript = @"
            document.getElementById('pbProcessing').className='pb-progress';
            document.getElementById('pbPreviewContent').style.display='none';
            document.getElementById('pbResult').className='pb-result pb-result--visible';
            openModal('modal-process-billing');";

        ScriptManager.RegisterStartupScript(this, GetType(), "showBillingResult", showScript, true);
    }

    // Inner class for billing info
    private class StudentBillingInfo
    {
        public string RegNo { get; set; }
        public string FullName { get; set; }
        public int StudyYear { get; set; }
        public int Semester { get; set; }
        public string Session { get; set; }
        public string RegStatus { get; set; }
        public double Tuition { get; set; }
        public double Functional { get; set; }
    }

    // Inner class for batch billing programme summary
    private class BatchProgSummary
    {
        public int PfId { get; set; }
        public string ProgCode { get; set; }
        public string ProgName { get; set; }
        public string Faculty { get; set; }
        public int UnbilledCount { get; set; }
        public int BilledCount { get; set; }
        public int NoFeeCount { get; set; }
        public double UnbilledAmount { get; set; }
        public double BilledAmount { get; set; }
        public Dictionary<string, double[]> FeeLookup { get; set; }
    }

    // ================================================================
    // BILL UNBILLED STUDENTS — Quick action
    // ================================================================

    protected void btnBillUnbilled_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "prog-fees";

        string acadYear = GetCurrentAcadYear();
        string currentUser = GetCurrentUser();
        if (string.IsNullOrEmpty(acadYear))
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "buErr",
                "showToast('No current academic year set.','error');", true);
            return;
        }
        if (string.IsNullOrEmpty(currentUser)) currentUser = "SYSTEM";

        string activeSemsCsv = GetActiveSemestersCsv(acadYear);
        if (activeSemsCsv == "0")
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "buErr",
                "showToast('No active semesters for " + acadYear.Replace("'", "") + ".','error');", true);
            return;
        }

        try
        {
            // 1. Find all unbilled students (active + registered this year + no bill)
            var toBill = new List<string[]>(); // each: [regno, semester]
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                string sql = string.Format(@"
                    SELECT DISTINCT r.regno, r.semester
                    FROM campus_dynamics.acad_registration r
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = r.regno AND s.new_status = 'ACTIVE'
                    WHERE r.acad_year = @ay
                      AND r.semester IN ({0})
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                      AND NOT EXISTS(
                          SELECT 1 FROM fin_studentfeestracking ft
                          WHERE ft.regno = r.regno AND ft.acadyear = r.acad_year
                            AND ft.semester = r.semester AND ft.trans_type = 'Bill'
                      )", activeSemsCsv);
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@ay", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            toBill.Add(new string[] {
                                rdr["regno"].ToString().Trim(),
                                rdr["semester"].ToString()
                            });
                        }
                    }
                }
            }

            if (toBill.Count == 0)
            {
                string msg0 = "No unbilled students found. All registered students are already billed.";
                ScriptManager.RegisterStartupScript(this, GetType(), "buDone",
                    "showToast('" + msg0.Replace("'", "\\'") + "','success');", true);
                LoadStudentOverviewStats();
                return;
            }

            // 2. Bill each student via fin_AutoBillOnRegistration SP
            int billed = 0, errors = 0;
            int skippedDup = 0;
            var errMsgs = new List<string>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                foreach (var stu in toBill)
                {
                    try
                    {
                        using (var cmd = new MySqlCommand("fin_AutoBillOnRegistration", conn))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.CommandTimeout = 30;
                            cmd.Parameters.AddWithValue("@p_regno", stu[0]);
                            cmd.Parameters.AddWithValue("@p_acadyear", acadYear);
                            cmd.Parameters.AddWithValue("@p_semester", Convert.ToInt32(stu[1]));
                            cmd.Parameters.AddWithValue("@p_user", currentUser);
                            // SP returns result sets that must be consumed
                            using (var rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read()) { }
                                while (rdr.NextResult()) { while (rdr.Read()) { } }
                            }
                        }
                        billed++;
                    }
                    catch (Exception ex)
                    {
                        // MySQL error 1062 = duplicate key — DB-level safety
                        var mex = ex as MySqlException;
                        if (mex != null && mex.Number == 1062)
                        {
                            skippedDup++;
                        }
                        else
                        {
                            errors++;
                            if (errMsgs.Count < 5)
                                errMsgs.Add(stu[0] + ": " + ex.Message);
                        }
                    }
                }
            }

            // 3. Show result
            string msg = billed + " student(s) billed successfully.";
            if (skippedDup > 0) msg += " " + skippedDup + " already billed (skipped).";
            if (errors > 0) msg += " " + errors + " error(s).";
            string toastType = errors > 0 ? "warning" : "success";

            ScriptManager.RegisterStartupScript(this, GetType(), "buDone",
                "showToast('" + msg.Replace("'", "\\'") + "','" + toastType + "');", true);

            LoadStudentOverviewStats();
        }
        catch (Exception ex)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "buErr",
                "showToast('Error: " + ex.Message.Replace("'", "\\'").Replace("\n", " ").Replace("\r", "") + "','error');", true);
        }
    }

    // ================================================================
    // BATCH BILLING — Preview & Execute (all programmes at once)
    // ================================================================

    private void LoadBatchBillingBadges()
    {
        try
        {
            string acadYear = GetCurrentAcadYear();
            litBBAcadYear.Text = string.IsNullOrEmpty(acadYear) ? "No Year Set" : Server.HtmlEncode(acadYear);
            if (!string.IsNullOrEmpty(acadYear))
                litBBActiveSems.Text = GetActiveSemestersDisplay(acadYear);
            else
                litBBActiveSems.Text = "None";
        }
        catch
        {
            litBBAcadYear.Text = "\u2014";
            litBBActiveSems.Text = "\u2014";
        }
    }

    /// <summary>
    /// Loads active semesters list for the given academic year.
    /// Returns e.g. "1,2" or "1,2,3" or "0" if none active.
    /// </summary>
    private string GetActiveSemestersCsv(string acadYear)
    {
        using (var conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                  FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@ay", acadYear);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        var sems = new List<string>();
                        if (rdr["semester_1_is_active"].ToString() == "Yes") sems.Add("1");
                        if (rdr["semester_2_is_active"].ToString() == "Yes") sems.Add("2");
                        if (rdr["semester_3_is_active"].ToString() == "Yes") sems.Add("3");
                        if (sems.Count > 0) return string.Join(",", sems.ToArray());
                    }
                }
            }
        }
        return "0";
    }

    private string GetActiveSemestersDisplay(string acadYear)
    {
        using (var conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                @"SELECT semester_1_is_active, semester_2_is_active, semester_3_is_active
                  FROM acad_acadyears WHERE acadyear = @ay LIMIT 1", conn))
            {
                cmd.Parameters.AddWithValue("@ay", acadYear);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        var active = new List<string>();
                        if (rdr["semester_1_is_active"].ToString() == "Yes") active.Add("Sem 1");
                        if (rdr["semester_2_is_active"].ToString() == "Yes") active.Add("Sem 2");
                        if (rdr["semester_3_is_active"].ToString() == "Yes") active.Add("Sem 3");
                        return active.Count > 0 ? string.Join(", ", active.ToArray()) : "None";
                    }
                }
            }
        }
        return "None";
    }

    /// <summary>
    /// Scans all active fee structures and generates a per-programme breakdown.
    /// </summary>
    protected void btnBBPreview_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "batch-billing";

        string acadYear = GetCurrentAcadYear();
        if (string.IsNullOrEmpty(acadYear))
        {
            ShowBBResult(false, "No Current Academic Year",
                "Please set a current academic year before running batch billing.", 0, 0, 0, 0);
            return;
        }

        string activeSemsCsv = GetActiveSemestersCsv(acadYear);
        if (activeSemsCsv == "0")
        {
            ShowBBResult(false, "No Active Semesters",
                "No semesters are marked as active for " + acadYear + ". Please activate semesters in Academic Year settings first.", 0, 0, 0, 0);
            return;
        }

        try
        {
            // 1) Load ALL active fee structures with their fee data
            var structures = new List<BatchProgSummary>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT pf.ID, pf.progcode, COALESCE(p.progname,'(Unknown)') AS progname,
                             COALESCE(fac.faculty_name,'') AS faculty_name,
                             pf.has_year_1, pf.has_year_2, pf.has_year_3,
                             pf.y1_s1_tuition, pf.y1_s1_functional, pf.y1_s2_tuition, pf.y1_s2_functional, pf.y1_s3_tuition, pf.y1_s3_functional,
                             pf.y2_s1_tuition, pf.y2_s1_functional, pf.y2_s2_tuition, pf.y2_s2_functional, pf.y2_s3_tuition, pf.y2_s3_functional,
                             pf.y3_s1_tuition, pf.y3_s1_functional, pf.y3_s2_tuition, pf.y3_s2_functional, pf.y3_s3_tuition, pf.y3_s3_functional
                      FROM fin_programme_fees pf
                      LEFT JOIN campus_dynamics.acad_programme p ON p.progcode = pf.progcode
                      LEFT JOIN campus_dynamics.acad_faculty fac ON fac.faculty_code = p.faculty_code
                      WHERE pf.is_active = 'Yes'
                      ORDER BY p.progname, pf.progcode", conn))
                {
                    cmd.CommandTimeout = 60;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var bps = new BatchProgSummary
                            {
                                PfId = Convert.ToInt32(rdr["ID"]),
                                ProgCode = rdr["progcode"].ToString().Trim(),
                                ProgName = rdr["progname"].ToString().Trim(),
                                Faculty = rdr["faculty_name"].ToString().Trim(),
                                FeeLookup = new Dictionary<string, double[]>()
                            };
                            for (int yr = 1; yr <= 3; yr++)
                            {
                                if (rdr[string.Format("has_year_{0}", yr)].ToString() != "Yes") continue;
                                for (int sem = 1; sem <= 3; sem++)
                                {
                                    double t = ToDouble(rdr[string.Format("y{0}_s{1}_tuition", yr, sem)]);
                                    double f = ToDouble(rdr[string.Format("y{0}_s{1}_functional", yr, sem)]);
                                    bps.FeeLookup[string.Format("{0}_{1}", yr, sem)] = new double[] { t, f };
                                }
                            }
                            structures.Add(bps);
                        }
                    }
                }
            }

            if (structures.Count == 0)
            {
                ShowBBResult(false, "No Active Fee Structures",
                    "There are no active fee structures to process. Please activate at least one fee structure first.", 0, 0, 0, 0);
                return;
            }

            // 2) For each programme, load registered students and existing bills
            //    Use batch queries grouped by programme codes
            var allProgCodes = new List<string>();
            foreach (var s in structures) allProgCodes.Add(s.ProgCode);

            // Load registered students (one big query with all programme codes)
            // Group by programme
            var studentsByProg = new Dictionary<string, List<StudentBillingInfo>>();
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                string inList = "'" + string.Join("','", allProgCodes.ToArray()) + "'";
                string regSql = string.Format(@"
                    SELECT r.regno, r.studyyear, r.semester,
                           s.progid, COALESCE(s.studsesion,'') AS studsesion
                    FROM acad_registration r
                    INNER JOIN acad_student s ON s.regno = r.regno
                    WHERE s.progid IN ({0})
                      AND r.acad_year = @acad
                      AND r.semester IN ({1})
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                    ORDER BY s.progid, r.semester", inList, activeSemsCsv);
                using (var cmd = new MySqlCommand(regSql, conn))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string prog = rdr["progid"].ToString().Trim();
                            if (!studentsByProg.ContainsKey(prog))
                                studentsByProg[prog] = new List<StudentBillingInfo>();

                            studentsByProg[prog].Add(new StudentBillingInfo
                            {
                                RegNo = rdr["regno"].ToString().Trim(),
                                StudyYear = Convert.ToInt32(rdr["studyyear"]),
                                Semester = Convert.ToInt32(rdr["semester"]),
                                Session = rdr["studsesion"].ToString().Trim()
                            });
                        }
                    }
                }
            }

            // Load existing bills (one big query for all these programmes)
            // Key = "regno|semester|itemcode"
            var existingBills = new HashSet<string>();
            var existingBillAmounts = new Dictionary<string, double>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                string inList = "'" + string.Join("','", allProgCodes.ToArray()) + "'";
                string billSql = string.Format(@"
                    SELECT ft.regno, ft.semester, ft.item_code, SUM(ft.amount) AS total_billed
                    FROM fin_studentfeestracking ft
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = ft.regno
                    WHERE s.progid IN ({0})
                      AND ft.acadyear = @acad
                      AND ft.trans_type = 'Bill'
                      AND ft.item_code IN (1, 52)
                    GROUP BY ft.regno, ft.semester, ft.item_code", inList);
                using (var cmd = new MySqlCommand(billSql, conn))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string bKey = string.Format("{0}|{1}|{2}",
                                rdr["regno"].ToString().Trim(),
                                Convert.ToInt32(rdr["semester"]),
                                Convert.ToInt32(rdr["item_code"]));
                            existingBills.Add(bKey);
                            existingBillAmounts[bKey] = Convert.ToDouble(rdr["total_billed"]);
                        }
                    }
                }
            }

            // 3) Classify per programme
            int totalUnbilled = 0, totalBilled = 0, totalNoFee = 0;
            double totalUnbilledAmt = 0, totalBilledAmt = 0;

            foreach (var bps in structures)
            {
                List<StudentBillingInfo> students;
                if (!studentsByProg.TryGetValue(bps.ProgCode, out students))
                    students = new List<StudentBillingInfo>();

                int progUnbilled = 0, progBilled = 0, progNoFee = 0;
                double progUnbilledAmt = 0, progBilledAmt = 0;

                foreach (var stu in students)
                {
                    string key = string.Format("{0}_{1}", stu.StudyYear, stu.Semester);
                    double tuit = 0, func = 0;
                    if (bps.FeeLookup.ContainsKey(key))
                    {
                        tuit = bps.FeeLookup[key][0];
                        func = bps.FeeLookup[key][1];
                    }

                    string tKey = string.Format("{0}|{1}|1", stu.RegNo, stu.Semester);
                    string fKey = string.Format("{0}|{1}|52", stu.RegNo, stu.Semester);
                    bool hasTuit = existingBills.Contains(tKey);
                    bool hasFunc = existingBills.Contains(fKey);

                    if (hasTuit && hasFunc)
                    {
                        progBilled++;
                        double bAmt = (existingBillAmounts.ContainsKey(tKey) ? existingBillAmounts[tKey] : 0)
                                    + (existingBillAmounts.ContainsKey(fKey) ? existingBillAmounts[fKey] : 0);
                        progBilledAmt += bAmt;
                    }
                    else if (tuit <= 0 && func <= 0 && !hasTuit && !hasFunc)
                    {
                        progNoFee++;
                    }
                    else
                    {
                        double remainT = hasTuit ? 0 : tuit;
                        double remainF = hasFunc ? 0 : func;
                        if (remainT > 0 || remainF > 0)
                        {
                            progUnbilled++;
                            progUnbilledAmt += remainT + remainF;
                        }
                        else
                        {
                            progBilled++;
                        }
                    }
                }

                bps.UnbilledCount = progUnbilled;
                bps.BilledCount = progBilled;
                bps.NoFeeCount = progNoFee;
                bps.UnbilledAmount = progUnbilledAmt;
                bps.BilledAmount = progBilledAmt;

                totalUnbilled += progUnbilled;
                totalBilled += progBilled;
                totalNoFee += progNoFee;
                totalUnbilledAmt += progUnbilledAmt;
                totalBilledAmt += progBilledAmt;
            }

            // 4) Build HTML table
            var tbl = new StringBuilder();
            tbl.Append("<table class='bb-table'><thead><tr>");
            tbl.Append("<th style='width:32px'>#</th>");
            tbl.Append("<th>Programme</th>");
            tbl.Append("<th style='text-align:right;width:90px'>Unbilled</th>");
            tbl.Append("<th style='text-align:right;width:90px'>Billed</th>");
            tbl.Append("<th style='text-align:right;width:80px'>No Fee</th>");
            tbl.Append("<th style='text-align:right;width:130px'>Amount to Bill</th>");
            tbl.Append("<th style='text-align:right;width:130px'>Already Billed</th>");
            tbl.Append("</tr></thead><tbody>");

            int rowNum = 0;
            int progsWithUnbilled = 0;
            foreach (var bps in structures)
            {
                // Only show programmes that have students
                if (bps.UnbilledCount == 0 && bps.BilledCount == 0 && bps.NoFeeCount == 0) continue;
                rowNum++;
                if (bps.UnbilledCount > 0) progsWithUnbilled++;

                string unbilledStyle = bps.UnbilledCount > 0 ? "color:#c62828;font-weight:800;" : "color:#999;";
                string billedStyle = bps.BilledCount > 0 ? "color:#155724;font-weight:700;" : "color:#999;";
                string noFeeStyle = bps.NoFeeCount > 0 ? "color:#b45309;font-weight:700;" : "color:#999;";

                string facHtml = !string.IsNullOrEmpty(bps.Faculty)
                    ? "<span class='bb-row-fac'>" + Server.HtmlEncode(bps.Faculty) + "</span>" : "";

                tbl.AppendFormat(
                    "<tr><td>{0}</td>"
                    + "<td><span class='bb-row-prog'>{1}</span><span class='bb-row-code'>{2}</span>{3}</td>"
                    + "<td class='bb-amt' style='{4}'>{5}</td>"
                    + "<td class='bb-amt' style='{6}'>{7}</td>"
                    + "<td class='bb-amt' style='{8}'>{9}</td>"
                    + "<td class='bb-amt' style='color:#6a1b9a;font-weight:700;'>{10:N0}</td>"
                    + "<td class='bb-amt' style='color:#155724;'>{11:N0}</td></tr>",
                    rowNum,
                    Server.HtmlEncode(bps.ProgName), Server.HtmlEncode(bps.ProgCode), facHtml,
                    unbilledStyle, bps.UnbilledCount,
                    billedStyle, bps.BilledCount,
                    noFeeStyle, bps.NoFeeCount,
                    bps.UnbilledAmount, bps.BilledAmount);
            }

            if (rowNum == 0)
            {
                tbl.Append("<tr><td colspan='7' style='text-align:center;padding:20px;color:#999;'>No registered students found for any active fee structure in the current academic year.</td></tr>");
            }

            tbl.Append("</tbody></table>");
            litBBPreviewTable.Text = tbl.ToString();

            // Set badge info
            litBBAcadYear.Text = Server.HtmlEncode(acadYear);
            litBBActiveSems.Text = GetActiveSemestersDisplay(acadYear);

            // JS to show the preview panel
            bool disableExecute = totalUnbilled == 0;
            string showJs = string.Format(
                @"_bbPreviewDone=true;
                  document.getElementById('bbLoading').className='bb-progress';
                  document.getElementById('bbPlaceholder').style.display='none';
                  document.getElementById('bbProcessing').className='bb-progress';
                  document.getElementById('bbResult').className='bb-result';
                  document.getElementById('bbPreviewContent').style.display='';
                  document.getElementById('bbStatProgs').innerText='{0}';
                  document.getElementById('bbStatUnbilled').innerText='{1}';
                  document.getElementById('bbStatBilled').innerText='{2}';
                  document.getElementById('bbStatNoFee').innerText='{3}';
                  document.getElementById('bbStatAmount').innerText='{4}';
                  document.getElementById('bbTableMeta').innerText='{5} programmes with students';
                  document.getElementById('btnBBGenPreview').disabled=false;
                  document.getElementById('btnBBExecute').disabled={6};
                  var tab=document.getElementById('tabBatchBilling');
                  if(tab) showPanel('batch-billing',tab);",
                structures.Count, totalUnbilled, totalBilled, totalNoFee,
                totalUnbilledAmt.ToString("N0"),
                rowNum,
                disableExecute ? "true" : "false");

            ScriptManager.RegisterStartupScript(this, GetType(), "showBBPreview", showJs, true);
        }
        catch (Exception ex)
        {
            ShowBBResult(false, "Preview Error",
                "An error occurred: " + ex.Message, 0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Executes batch billing across ALL active fee structures.
    /// </summary>
    protected void btnBBExecute_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "batch-billing";

        string acadYear = GetCurrentAcadYear();
        string currentUser = GetCurrentUser();
        if (string.IsNullOrEmpty(acadYear))
        {
            ShowBBResult(false, "No Current Academic Year",
                "Please set a current academic year first.", 0, 0, 0, 0);
            return;
        }
        if (string.IsNullOrEmpty(currentUser)) currentUser = "SYSTEM";

        string activeSemsCsv = GetActiveSemestersCsv(acadYear);
        if (activeSemsCsv == "0")
        {
            ShowBBResult(false, "No Active Semesters",
                "No semesters are active for " + acadYear + ".", 0, 0, 0, 0);
            return;
        }

        try
        {
            // 1) Load all active fee structures
            var structures = new List<BatchProgSummary>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(
                    @"SELECT pf.ID, pf.progcode,
                             pf.has_year_1, pf.has_year_2, pf.has_year_3,
                             pf.y1_s1_tuition, pf.y1_s1_functional, pf.y1_s2_tuition, pf.y1_s2_functional, pf.y1_s3_tuition, pf.y1_s3_functional,
                             pf.y2_s1_tuition, pf.y2_s1_functional, pf.y2_s2_tuition, pf.y2_s2_functional, pf.y2_s3_tuition, pf.y2_s3_functional,
                             pf.y3_s1_tuition, pf.y3_s1_functional, pf.y3_s2_tuition, pf.y3_s2_functional, pf.y3_s3_tuition, pf.y3_s3_functional
                      FROM fin_programme_fees pf
                      WHERE pf.is_active = 'Yes'
                      ORDER BY pf.progcode", conn))
                {
                    cmd.CommandTimeout = 60;
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var bps = new BatchProgSummary
                            {
                                PfId = Convert.ToInt32(rdr["ID"]),
                                ProgCode = rdr["progcode"].ToString().Trim(),
                                FeeLookup = new Dictionary<string, double[]>()
                            };
                            for (int yr = 1; yr <= 3; yr++)
                            {
                                if (rdr[string.Format("has_year_{0}", yr)].ToString() != "Yes") continue;
                                for (int sem = 1; sem <= 3; sem++)
                                {
                                    double t = ToDouble(rdr[string.Format("y{0}_s{1}_tuition", yr, sem)]);
                                    double f = ToDouble(rdr[string.Format("y{0}_s{1}_functional", yr, sem)]);
                                    bps.FeeLookup[string.Format("{0}_{1}", yr, sem)] = new double[] { t, f };
                                }
                            }
                            structures.Add(bps);
                        }
                    }
                }
            }

            if (structures.Count == 0)
            {
                ShowBBResult(false, "No Active Fee Structures",
                    "No active fee structures found.", 0, 0, 0, 0);
                return;
            }

            // 2) Load all registered students for these programmes
            var allProgCodes = new List<string>();
            foreach (var s in structures) allProgCodes.Add(s.ProgCode);

            var studentsByProg = new Dictionary<string, List<StudentBillingInfo>>();
            using (var conn = new MySqlConnection(MainConnStr))
            {
                conn.Open();
                string inList = "'" + string.Join("','", allProgCodes.ToArray()) + "'";
                string sql = string.Format(@"
                    SELECT r.regno, r.studyyear, r.semester,
                           s.progid, COALESCE(s.studsesion,'') AS studsesion
                    FROM acad_registration r
                    INNER JOIN acad_student s ON s.regno = r.regno
                    WHERE s.progid IN ({0})
                      AND r.acad_year = @acad
                      AND r.semester IN ({1})
                      AND r.regstatus IN ('REGISTERED','LATE REGISTERED','LATE-REGISTERED','CLEARED')
                    ORDER BY s.progid", inList, activeSemsCsv);
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string prog = rdr["progid"].ToString().Trim();
                            if (!studentsByProg.ContainsKey(prog))
                                studentsByProg[prog] = new List<StudentBillingInfo>();
                            studentsByProg[prog].Add(new StudentBillingInfo
                            {
                                RegNo = rdr["regno"].ToString().Trim(),
                                StudyYear = Convert.ToInt32(rdr["studyyear"]),
                                Semester = Convert.ToInt32(rdr["semester"]),
                                Session = rdr["studsesion"].ToString().Trim()
                            });
                        }
                    }
                }
            }

            // 3) Load existing bills for the CURRENT academic year only.
            //    Each academic year must be billed independently — a student
            //    billed in 2024/2025 Sem 1 still needs a 2025/2026 Sem 1 bill.
            var existingBills = new HashSet<string>();
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                string inList = "'" + string.Join("','", allProgCodes.ToArray()) + "'";
                string sql = string.Format(@"
                    SELECT ft.regno, ft.semester, ft.item_code
                    FROM fin_studentfeestracking ft
                    INNER JOIN campus_dynamics.acad_student s ON s.regno = ft.regno
                    WHERE s.progid IN ({0})
                      AND ft.acadyear = @acad
                      AND ft.trans_type = 'Bill'
                      AND ft.item_code IN (1, 52)
                    GROUP BY ft.regno, ft.semester, ft.item_code", inList);
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = 120;
                    cmd.Parameters.AddWithValue("@acad", acadYear);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            string bKey = string.Format("{0}|{1}|{2}",
                                rdr["regno"].ToString().Trim(),
                                Convert.ToInt32(rdr["semester"]),
                                Convert.ToInt32(rdr["item_code"]));
                            existingBills.Add(bKey);
                        }
                    }
                }
            }

            // 4) Process billing programme by programme
            int totalBilledStudents = 0;
            int totalSkippedAlready = 0;
            int totalSkippedNoFee = 0;
            int totalErrors = 0;
            int progsProcessed = 0;
            double totalBilledAmount = 0;
            var errors = new List<string>();

            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();

                foreach (var bps in structures)
                {
                    List<StudentBillingInfo> students;
                    if (!studentsByProg.TryGetValue(bps.ProgCode, out students))
                        continue; // no registered students

                    bool anyBilled = false;
                    foreach (var stu in students)
                    {
                        string key = string.Format("{0}_{1}", stu.StudyYear, stu.Semester);
                        double tuit = 0, func = 0;
                        if (bps.FeeLookup.ContainsKey(key))
                        {
                            tuit = bps.FeeLookup[key][0];
                            func = bps.FeeLookup[key][1];
                        }

                        if (tuit <= 0 && func <= 0)
                        {
                            totalSkippedNoFee++;
                            continue;
                        }

                        string tKey = string.Format("{0}|{1}|1", stu.RegNo, stu.Semester);
                        string fKey = string.Format("{0}|{1}|52", stu.RegNo, stu.Semester);
                        bool tuitBilled = existingBills.Contains(tKey);
                        bool funcBilled = existingBills.Contains(fKey);

                        if (tuitBilled && funcBilled)
                        {
                            totalSkippedAlready++;
                            continue;
                        }

                        try
                        {
                            using (var cmd = new MySqlCommand(
                                "CALL fin_BillProgrammeFees(@reg, @prog, @sess, @yr, @sem, @acad, @user, @csid)", conn))
                            {
                                cmd.CommandTimeout = 30;
                                cmd.Parameters.AddWithValue("@reg", stu.RegNo);
                                cmd.Parameters.AddWithValue("@prog", bps.ProgCode);
                                cmd.Parameters.AddWithValue("@sess", string.IsNullOrEmpty(stu.Session) ? "Day" : stu.Session);
                                cmd.Parameters.AddWithValue("@yr", stu.StudyYear);
                                cmd.Parameters.AddWithValue("@sem", stu.Semester);
                                cmd.Parameters.AddWithValue("@acad", acadYear);
                                cmd.Parameters.AddWithValue("@user", currentUser);
                                cmd.Parameters.AddWithValue("@csid", "BATCH");
                                cmd.ExecuteNonQuery();
                            }

                            totalBilledStudents++;
                            if (!tuitBilled) totalBilledAmount += tuit;
                            if (!funcBilled) totalBilledAmount += func;
                            anyBilled = true;

                            // Mark as billed in memory to avoid re-processing
                            existingBills.Add(tKey);
                            existingBills.Add(fKey);
                        }
                        catch (Exception ex)
                        {
                            // MySQL error 1062 = duplicate key — DB-level uniqueness
                            // constraint caught a race condition. Safe; count as skipped.
                            var mex = ex as MySqlException;
                            if (mex != null && mex.Number == 1062)
                            {
                                totalSkippedAlready++;
                                existingBills.Add(tKey);
                                existingBills.Add(fKey);
                            }
                            else
                            {
                                totalErrors++;
                                if (errors.Count < 10)
                                    errors.Add(string.Format("{0} [{1}] Yr{2}/S{3}: {4}",
                                        stu.RegNo, bps.ProgCode, stu.StudyYear, stu.Semester, ex.Message));
                            }
                        }
                    }
                    if (anyBilled) progsProcessed++;
                }
            }

            // 5) Show result
            if (totalErrors == 0 && totalBilledStudents > 0)
            {
                string detail = string.Format(
                    "{0} student(s) billed across {1} programme(s). Total amount: UGX {2:N0}.",
                    totalBilledStudents, progsProcessed, totalBilledAmount);
                if (totalSkippedAlready > 0)
                    detail += string.Format(" {0} already billed (skipped).", totalSkippedAlready);
                if (totalSkippedNoFee > 0)
                    detail += string.Format(" {0} no fee defined (skipped).", totalSkippedNoFee);

                ShowBBResult(true, string.Format("Batch Billing Complete \u2014 {0} Students Processed", totalBilledStudents),
                    detail, totalBilledStudents, progsProcessed, totalSkippedAlready, totalBilledAmount);
            }
            else if (totalErrors == 0 && totalBilledStudents == 0)
            {
                string detail = "All students are already billed or have no fee structures.";
                if (totalSkippedAlready > 0) detail += string.Format(" {0} already billed.", totalSkippedAlready);
                if (totalSkippedNoFee > 0) detail += string.Format(" {0} no fee defined.", totalSkippedNoFee);
                ShowBBResult(true, "No New Billing Needed", detail, 0, 0, totalSkippedAlready, 0);
            }
            else
            {
                string detail = string.Format(
                    "{0} billed, {1} error(s), {2} already billed, {3} no-fee skipped.",
                    totalBilledStudents, totalErrors, totalSkippedAlready, totalSkippedNoFee);
                if (errors.Count > 0) detail += " Errors: " + string.Join("; ", errors.ToArray());
                ShowBBResult(false, "Batch Billing Completed with Errors",
                    detail, totalBilledStudents, progsProcessed, totalSkippedAlready, totalBilledAmount);
            }
        }
        catch (Exception ex)
        {
            ShowBBResult(false, "Batch Billing Error",
                "An unexpected error occurred: " + ex.Message, 0, 0, 0, 0);
        }
    }

    private void ShowBBResult(bool success, string title, string detail,
        int billedCount, int progsCount, int skippedCount, double amount)
    {
        string iconSvg = success
            ? "<svg class='bb-result__icon' viewBox='0 0 24 24' fill='none' stroke='#2e7d32' stroke-width='2'><path d='M22 11.08V12a10 10 0 1 1-5.93-9.14'/><polyline points='22 4 12 14.01 9 11.01'/></svg>"
            : "<svg class='bb-result__icon' viewBox='0 0 24 24' fill='none' stroke='#c62828' stroke-width='2'><circle cx='12' cy='12' r='10'/><line x1='15' y1='9' x2='9' y2='15'/><line x1='9' y1='9' x2='15' y2='15'/></svg>";

        string titleClass = success ? "bb-result__title bb-result__title--success" : "bb-result__title bb-result__title--error";

        var html = new StringBuilder();
        html.Append(iconSvg);
        html.AppendFormat("<div class='{0}'>{1}</div>", titleClass, Server.HtmlEncode(title));
        html.AppendFormat("<div class='bb-result__detail'>{0}</div>", Server.HtmlEncode(detail));

        if (billedCount > 0 || progsCount > 0)
        {
            html.Append("<div class='bb-result__stats'>");
            html.AppendFormat("<div class='bb-result__stat'><div class='bb-result__stat-val'>{0}</div><div class='bb-result__stat-lbl'>Students Billed</div></div>", billedCount);
            html.AppendFormat("<div class='bb-result__stat'><div class='bb-result__stat-val'>{0}</div><div class='bb-result__stat-lbl'>Programmes</div></div>", progsCount);
            html.AppendFormat("<div class='bb-result__stat'><div class='bb-result__stat-val'>{0:N0}</div><div class='bb-result__stat-lbl'>Amount (UGX)</div></div>", amount);
            if (skippedCount > 0)
                html.AppendFormat("<div class='bb-result__stat'><div class='bb-result__stat-val'>{0}</div><div class='bb-result__stat-lbl'>Skipped</div></div>", skippedCount);
            html.Append("</div>");
        }

        html.Append("<div style='margin-top:16px;'><button type='button' class='fs-btn fs-btn--primary' onclick=\"var t=document.getElementById('tabBatchBilling');showPanel('batch-billing',t);document.getElementById('bbResult').className='bb-result';document.getElementById('bbPlaceholder').style.display='';document.getElementById('bbPreviewContent').style.display='none';\">Done</button></div>");

        litBBResult.Text = html.ToString();

        string showJs = @"
            document.getElementById('bbProcessing').className='bb-progress';
            document.getElementById('bbLoading').className='bb-progress';
            document.getElementById('bbPreviewContent').style.display='none';
            document.getElementById('bbResult').className='bb-result bb-result--visible';
            document.getElementById('btnBBGenPreview').disabled=false;
            var tab=document.getElementById('tabBatchBilling');
            if(tab) showPanel('batch-billing',tab);";

        ScriptManager.RegisterStartupScript(this, GetType(), "showBBResult", showJs, true);
    }

    // ================================================================
    // BILLING ITEMS (kept from old version)
    // ================================================================

    private void LoadBillingItems()
    {
        var sb = new StringBuilder();
        int count = 0;
        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("SELECT * FROM academicbillingitems ORDER BY PriorityCode DESC, ItemCode", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    count++;
                    int code = Convert.ToInt32(rdr["ItemCode"]);
                    string name = rdr["ItemName"].ToString();
                    string acct = Nvl(rdr["AccountCode"]);
                    int prio = Convert.ToInt32(rdr["PriorityCode"]);
                    string prioBadge = prio > 0
                        ? "<span class='fs-badge fs-badge--green'>Core</span>"
                        : "<span class='fs-badge fs-badge--amber'>Optional</span>";

                    sb.AppendFormat(
                        "<tr><td><span class='fs-code'>{0}</span></td><td style='font-weight:600;'>{1}</td>"
                        + "<td><span class='fs-code'>{2}</span></td><td>{3}</td>"
                        + "<td><a class='fs-row-action' href='javascript:void(0)' onclick=\"editBillingItem({0},'{4}','{5}','{6}')\">Edit</a>"
                        + " <a class='fs-row-action fs-row-action--danger' href='javascript:void(0)' onclick=\"deleteRow({0},'BI','{4}')\">Del</a></td></tr>",
                        code, Server.HtmlEncode(name), Server.HtmlEncode(acct), prioBadge,
                        JsEsc(name), JsEsc(acct), prio);
                }
            }
        }
        litBillingItems.Text = sb.ToString();
        litBillingItemCount.Text = string.Format("{0} items", count);
    }

    // ================================================================
    // BILLING SYSTEMS (kept from old version)
    // ================================================================

    private void LoadBillingSystems()
    {
        var sb = new StringBuilder();
        int count = 0;
        using (var conn = new MySqlConnection(AcctConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand("SELECT * FROM fin_billing_systems ORDER BY ID", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    count++;
                    int id = Convert.ToInt32(rdr["ID"]);
                    string name = rdr["bs_name"].ToString();
                    string desc = Nvl(rdr["bs_description"]);
                    string curr = Nvl(rdr["bs_currency"]);

                    sb.AppendFormat(
                        "<tr><td><span class='fs-code'>{0}</span></td><td style='font-weight:600;'>{1}</td>"
                        + "<td>{2}</td><td><span class='fs-badge fs-badge--primary'>{3}</span></td>"
                        + "<td><a class='fs-row-action' href='javascript:void(0)' onclick=\"editBillingSystem({0},'{4}','{5}','{6}')\">Edit</a>"
                        + " <a class='fs-row-action fs-row-action--danger' href='javascript:void(0)' onclick=\"deleteRow({0},'BS','{4}')\">Del</a></td></tr>",
                        id, Server.HtmlEncode(name), Server.HtmlEncode(desc), Server.HtmlEncode(curr),
                        JsEsc(name), JsEsc(desc), JsEsc(curr));
                }
            }
        }
        litSystemRows.Text = sb.ToString();
        litSystemCount.Text = string.Format("{0} systems", count);
    }

    // ================================================================
    // SAVE BILLING ITEM (kept from old version)
    // ================================================================

    protected void btnSaveBillingItem_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "billing-items";
        string name = txtBIName.Text.Trim();
        string acct = txtBIAccount.Text.Trim();
        int prio = Convert.ToInt32(ddlBIPriority.SelectedValue);
        string editId = hfEditId.Value;

        if (string.IsNullOrEmpty(name))
        {
            ShowToast("Item Name is required.", false);
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                if (string.IsNullOrEmpty(editId))
                {
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO academicbillingitems (ItemName,AccountCode,PriorityCode) VALUES (@n,@a,@p)", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@a", acct);
                        cmd.Parameters.AddWithValue("@p", prio);
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast("Billing item added.", true);
                }
                else
                {
                    using (var cmd = new MySqlCommand(
                        "UPDATE academicbillingitems SET ItemName=@n,AccountCode=@a,PriorityCode=@p WHERE ItemCode=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@a", acct);
                        cmd.Parameters.AddWithValue("@p", prio);
                        cmd.Parameters.AddWithValue("@id", Convert.ToInt32(editId));
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast(string.Format("Billing item #{0} updated.", editId), true);
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + ex.Message, false);
        }
        hfEditId.Value = "";
    }

    // ================================================================
    // SAVE BILLING SYSTEM (kept from old version)
    // ================================================================

    protected void btnSaveBillingSystem_Click(object sender, EventArgs e)
    {
        hfActivePanel.Value = "billing-systems";
        string name = txtBSName.Text.Trim();
        string desc = txtBSDesc.Text.Trim();
        string curr = txtBSCurrency.Text.Trim();
        string editId = hfEditId.Value;

        if (string.IsNullOrEmpty(name))
        {
            ShowToast("System Name is required.", false);
            return;
        }

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                if (string.IsNullOrEmpty(editId))
                {
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO fin_billing_systems (bs_name,bs_description,bs_currency) VALUES (@n,@d,@c)", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@d", desc);
                        cmd.Parameters.AddWithValue("@c", curr);
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast("Billing system added.", true);
                }
                else
                {
                    using (var cmd = new MySqlCommand(
                        "UPDATE fin_billing_systems SET bs_name=@n,bs_description=@d,bs_currency=@c WHERE ID=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@n", name);
                        cmd.Parameters.AddWithValue("@d", desc);
                        cmd.Parameters.AddWithValue("@c", curr);
                        cmd.Parameters.AddWithValue("@id", Convert.ToInt32(editId));
                        cmd.ExecuteNonQuery();
                    }
                    ShowToast(string.Format("Billing system #{0} updated.", editId), true);
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error: " + ex.Message, false);
        }
        hfEditId.Value = "";
    }

    // ================================================================
    // DELETE ROW (any entity type)
    // ================================================================

    protected void btnDeleteRow_Click(object sender, EventArgs e)
    {
        string editId = hfEditId.Value;
        string editType = hfEditType.Value;
        string sql;

        switch (editType)
        {
            case "BI":
                sql = "DELETE FROM academicbillingitems WHERE ItemCode=@id";
                hfActivePanel.Value = "billing-items";
                break;
            case "BS":
                sql = "DELETE FROM fin_billing_systems WHERE ID=@id";
                hfActivePanel.Value = "billing-systems";
                break;
            case "PF":
                sql = "DELETE FROM fin_programme_fees WHERE ID=@id";
                hfActivePanel.Value = "prog-fees";
                break;
            default:
                ShowToast("Unknown entity type: " + editType, false);
                return;
        }

        try
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", Convert.ToInt32(editId));
                    int rows = cmd.ExecuteNonQuery();
                    ShowToast(rows > 0 ? "Record deleted." : "Record not found.", rows > 0);
                }
            }
        }
        catch (Exception ex)
        {
            ShowToast("Error deleting: " + ex.Message, false);
        }
        hfEditId.Value = "";
    }

    // ================================================================
    // DROPDOWN POPULATION
    // ================================================================

    private void PopulatePFProgrammeDropdown(string includeProgcode)
    {
        // Load all programmes from acad_programme
        var allProgs = new List<ListItem>();
        using (var conn = new MySqlConnection(MainConnStr))
        {
            conn.Open();
            using (var cmd = new MySqlCommand(
                "SELECT progcode, progname FROM acad_programme ORDER BY progname", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    allProgs.Add(new ListItem(
                        string.Format("{0} - {1}", rdr["progcode"].ToString().Trim(), rdr["progname"]),
                        rdr["progcode"].ToString().Trim()));
                }
            }
        }

        // Get programmes that already have a fee structure (exclude from Add)
        var existing = new HashSet<string>();
        if (string.IsNullOrEmpty(includeProgcode))
        {
            using (var conn = new MySqlConnection(AcctConnStr))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT progcode FROM fin_programme_fees", conn))
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                        existing.Add(rdr["progcode"].ToString().Trim());
                }
            }
        }

        ddlPFProg.Items.Clear();
        ddlPFProg.Items.Add(new ListItem("-- Select Programme --", ""));

        foreach (var li in allProgs)
        {
            // If adding (includeProgcode is empty), skip programmes that already have structures
            // If editing (includeProgcode is set), include all programmes
            if (!string.IsNullOrEmpty(includeProgcode) || !existing.Contains(li.Value))
                ddlPFProg.Items.Add(li);
        }
    }

    private static void SetDdl(DropDownList ddl, string val)
    {
        if (!string.IsNullOrEmpty(val) && ddl.Items.FindByValue(val) != null)
            ddl.SelectedValue = val;
    }

    // ================================================================
    // HELPERS
    // ================================================================

    private void ShowToast(string message, bool success)
    {
        pnlToast.Visible = true;
        divToast.Attributes["class"] = success ? "fs-toast fs-toast--success" : "fs-toast fs-toast--error";
        divToast.InnerHtml = Server.HtmlEncode(message);
    }

    private static string Nvl(object val)
    {
        return val == null || val == DBNull.Value ? "" : val.ToString();
    }

    private static string JsEsc(string val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "\\n");
    }

    private static double ToDouble(object val)
    {
        if (val == null || val == DBNull.Value) return 0;
        double d;
        if (double.TryParse(val.ToString(), out d)) return d;
        return 0;
    }

    private static double ParseAmt(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        string clean = text.Trim().Replace(",", "");
        double d;
        if (double.TryParse(clean, out d)) return d;
        return 0;
    }

    private string GetCurrentUser()
    {
        if (Session["user"] != null) return Session["user"].ToString();
        if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            return User.Identity.Name;
        return "SYSTEM";
    }
}
