using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Web.Script.Serialization;
using System.Web.UI;
using System.Web.UI.WebControls;
using MySql.Data.MySqlClient;

/// <summary>
/// Admin view of all retake registrations — original vs new marks, fee status, marks stage.
/// Read/monitor + CSV export. Data: campus_dynamics_portal.acad_retake_registrations.
/// </summary>
public partial class COOPERP_NewScreens_RetakeController : System.Web.UI.Page
{
    private string Conn { get { return ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString; } }
    private const string RR = "campus_dynamics_portal.acad_retake_registrations";
    private const string CR = "campus_dynamics_portal.acad_course_registration";

    protected void Page_Load(object sender, EventArgs e)
    {
        if (!IsPostBack)
        {
            LoadAcademicYears();
            ApplyQueryString();
            LoadStats();
            BindGrid();
        }
    }

    private void LoadAcademicYears()
    {
        ddlYear.Items.Clear();
        ddlYear.Items.Add(new ListItem("All Years", ""));
        try
        {
            using (var conn = new MySqlConnection(Conn))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT DISTINCT retake_acad_year FROM " + RR + " WHERE TRIM(IFNULL(retake_acad_year,''))<>'' ORDER BY retake_acad_year DESC", conn))
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read()) ddlYear.Items.Add(new ListItem(rdr[0].ToString(), rdr[0].ToString()));
            }
        }
        catch { }
    }

    private void ApplyQueryString()
    {
        SetSel(ddlYear, Request.QueryString["ay"]);
        SetSel(ddlSem, Request.QueryString["sem"]);
        SetSel(ddlStatus, Request.QueryString["st"]);
        if (!string.IsNullOrEmpty(Request.QueryString["q"])) txtSearch.Text = Request.QueryString["q"];
    }
    private void SetSel(ListControl c, string v)
    {
        if (c == null || string.IsNullOrEmpty(v)) return;
        var it = c.Items.FindByValue(v);
        if (it != null) { c.ClearSelection(); it.Selected = true; }
    }

    private string BuildWhere(List<MySqlParameter> ps)
    {
        var sb = new StringBuilder(" WHERE 1=1");
        if (!string.IsNullOrEmpty(ddlYear.SelectedValue)) { sb.Append(" AND rr.retake_acad_year=@ay"); ps.Add(new MySqlParameter("@ay", ddlYear.SelectedValue)); }
        if (!string.IsNullOrEmpty(ddlSem.SelectedValue)) { sb.Append(" AND rr.retake_semester=@sem"); ps.Add(new MySqlParameter("@sem", SafeInt(ddlSem.SelectedValue, 0))); }
        if (!string.IsNullOrEmpty(ddlStatus.SelectedValue)) { sb.Append(" AND rr.status=@st"); ps.Add(new MySqlParameter("@st", ddlStatus.SelectedValue)); }
        string q = (txtSearch.Text ?? "").Trim();
        if (!string.IsNullOrEmpty(q))
        {
            sb.Append(" AND (rr.regno LIKE @q OR rr.courseID LIKE @q OR rr.course_name LIKE @q OR TRIM(CONCAT_WS(' ',s.firstname,s.othername)) LIKE @q)");
            ps.Add(new MySqlParameter("@q", "%" + q + "%"));
        }
        return sb.ToString();
    }

    private void LoadStats()
    {
        var ps = new List<MySqlParameter>();
        string where = BuildWhere(ps);
        string sql = @"SELECT COUNT(*) total,
            SUM(rr.status='COMPLETED') completed,
            SUM(rr.status NOT IN ('COMPLETED','CANCELLED')) active,
            SUM(rr.fee_billed='Yes') billed,
            COALESCE(SUM(CASE WHEN rr.fee_billed='Yes' THEN rr.retake_fee ELSE 0 END),0) fee
            FROM " + RR + @" rr LEFT JOIN acad_student s ON TRIM(s.regno)=TRIM(rr.regno)" + where;
        try
        {
            using (var conn = new MySqlConnection(Conn))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var p in ps) cmd.Parameters.Add(p);
                    using (var rdr = cmd.ExecuteReader())
                        if (rdr.Read())
                        {
                            litTotal.Text = N(rdr["total"]);
                            litActive.Text = N(rdr["active"]);
                            litCompleted.Text = N(rdr["completed"]);
                            litBilled.Text = N(rdr["billed"]);
                            litFee.Text = "UGX " + N(rdr["fee"]);
                        }
                }
            }
        }
        catch { }
    }

    private void BindGrid()
    {
        var ps = new List<MySqlParameter>();
        string where = BuildWhere(ps);
        string sql = @"
            SELECT rr.ID, rr.regno,
                   TRIM(CONCAT_WS(' ', NULLIF(s.firstname,''), NULLIF(s.othername,''))) AS name,
                   rr.courseID, rr.course_name, rr.prog_id, rr.attempt_no,
                   rr.orig_acad_year, rr.orig_semester, rr.orig_grade, rr.orig_total,
                   rr.retake_acad_year, rr.retake_semester, rr.fee_billed, rr.status,
                   rr.new_grade, rr.new_total, rr.registered_by,
                   DATE_FORMAT(rr.registered_at,'%d %b %Y') AS reg_date,
                   COALESCE(cr.mark_stage,'NOT_ENTERED') AS stage
            FROM " + RR + @" rr
            LEFT JOIN acad_student s ON TRIM(s.regno)=TRIM(rr.regno)
            LEFT JOIN " + CR + @" cr ON cr.ID=rr.course_reg_id" + where + @"
            ORDER BY rr.registered_at DESC, rr.ID DESC
            LIMIT 1000";
        try
        {
            using (var conn = new MySqlConnection(Conn))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var p in ps) cmd.Parameters.Add(p);
                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);
                        rpt.DataSource = dt;
                        rpt.DataBind();
                        pnlEmpty.Visible = dt.Rows.Count == 0;
                        litCount.Text = dt.Rows.Count.ToString("N0") + (dt.Rows.Count == 1000 ? "+ (capped)" : "") + " retake(s)";
                    }
                }
            }
        }
        catch (Exception ex) { litCount.Text = "Error: " + Server.HtmlEncode(ex.Message); }
    }

    protected void btnFilter_Click(object sender, EventArgs e) { RedirectWithFilters(); }
    protected void btnReset_Click(object sender, EventArgs e) { Response.Redirect("RetakeController.aspx"); }

    private void RedirectWithFilters()
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(ddlYear.SelectedValue)) q["ay"] = ddlYear.SelectedValue;
        if (!string.IsNullOrEmpty(ddlSem.SelectedValue)) q["sem"] = ddlSem.SelectedValue;
        if (!string.IsNullOrEmpty(ddlStatus.SelectedValue)) q["st"] = ddlStatus.SelectedValue;
        if (!string.IsNullOrEmpty(txtSearch.Text.Trim())) q["q"] = txtSearch.Text.Trim();
        string s = q.ToString();
        Response.Redirect("RetakeController.aspx" + (string.IsNullOrEmpty(s) ? "" : "?" + s));
    }

    protected void btnExport_Click(object sender, EventArgs e)
    {
        var ps = new List<MySqlParameter>();
        string where = BuildWhere(ps);
        string sql = @"
            SELECT rr.regno, TRIM(CONCAT_WS(' ', NULLIF(s.firstname,''), NULLIF(s.othername,''))) AS name,
                   rr.courseID, rr.course_name, rr.prog_id, rr.attempt_no,
                   rr.orig_acad_year, rr.orig_semester, rr.orig_total, rr.orig_grade,
                   rr.retake_acad_year, rr.retake_semester, rr.fee_billed, rr.retake_fee, rr.status,
                   rr.new_total, rr.new_grade, COALESCE(cr.mark_stage,'NOT_ENTERED') AS stage,
                   rr.registered_by, DATE_FORMAT(rr.registered_at,'%Y-%m-%d') AS reg_date
            FROM " + RR + @" rr
            LEFT JOIN acad_student s ON TRIM(s.regno)=TRIM(rr.regno)
            LEFT JOIN " + CR + @" cr ON cr.ID=rr.course_reg_id" + where + " ORDER BY rr.registered_at DESC";
        try
        {
            using (var conn = new MySqlConnection(Conn))
            {
                conn.Open();
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    foreach (var p in ps) cmd.Parameters.Add(p);
                    using (var da = new MySqlDataAdapter(cmd))
                    {
                        var dt = new DataTable(); da.Fill(dt);
                        var csv = new StringBuilder();
                        csv.AppendLine("Reg No,Student,Course Code,Course Name,Programme,Attempt,Orig Year,Orig Sem,Orig Total,Orig Grade,Retake Year,Retake Sem,Fee Billed,Retake Fee,Status,New Total,New Grade,Marks Stage,Registered By,Registered On");
                        foreach (DataRow r in dt.Rows)
                            csv.AppendLine(string.Join(",",
                                C(r["regno"]), C(r["name"]), C(r["courseID"]), C(r["course_name"]), C(r["prog_id"]), C(r["attempt_no"]),
                                C(r["orig_acad_year"]), C(r["orig_semester"]), C(r["orig_total"]), C(r["orig_grade"]),
                                C(r["retake_acad_year"]), C(r["retake_semester"]), C(r["fee_billed"]), C(r["retake_fee"]), C(r["status"]),
                                C(r["new_total"]), C(r["new_grade"]), C(r["stage"]), C(r["registered_by"]), C(r["reg_date"])));
                        Response.Clear();
                        Response.ContentType = "text/csv";
                        Response.AddHeader("Content-Disposition", "attachment; filename=retakes.csv");
                        Response.Write(csv.ToString());
                        Response.End();
                    }
                }
            }
        }
        catch (Exception ex) { litCount.Text = "Export failed: " + Server.HtmlEncode(ex.Message); }
    }

    // Reversible until the retake result is OFFICIAL. Provisional in-progress marks
    // (ENTERED / CAPTURED / APPROVED) can still be reversed — they are discarded and the
    // original result restored. Only a PUBLISHED (or COMPLETED) retake is locked, because
    // that has already written to acad_results / GPA.
    protected bool CanReverse(object stage, object status, object newGrade)
    {
        string st = (stage == null ? "" : stage.ToString()).Trim().ToUpperInvariant();
        string status2 = (status == null ? "" : status.ToString()).Trim().ToUpperInvariant();
        string ng = (newGrade == null ? "" : newGrade.ToString()).Trim();
        if (status2 == "COMPLETED") return false;
        if (st == "PUBLISHED") return false;
        if (ng != "") return false;
        return true;
    }

    // ── Reverse / delete a retake registration (admin) ──────────────────────────
    // Undoes EVERYTHING the registration created, in one cross-DB transaction:
    //   1. reverses the retake fee — reduces (or deletes) the accumulated item-21 Bill in
    //      fin_studentfeestracking and removes this retake's DR(student)/CR(revenue) GL pair,
    //   2. deletes the RT course-registration row,
    //   3. deletes the acad_retake_registrations row,
    //   4. writes an audit-log entry.
    // Blocked once the retake has any marks (see CanReverse). WebMethods bypass the master
    // page, so we guard the session explicitly.
    [WebMethod(EnableSession = true)]
    public static string ReverseRetake(int id, string reason)
    {
        var js = new JavaScriptSerializer();
        var ctx = HttpContext.Current;
        if (ctx == null || ctx.Session == null || ctx.Session["username"] == null)
            return js.Serialize(new { ok = false, message = "Your session has expired. Please sign in again." });
        if (id <= 0) return js.Serialize(new { ok = false, message = "Invalid retake reference." });
        string actor = ctx.Session["username"].ToString();
        string connStr = ConfigurationManager.ConnectionStrings["vacConnectionString"].ConnectionString;

        try
        {
            using (var conn = new MySqlConnection(connStr))
            {
                conn.Open();

                string regno = "", courseID = "", feeBilled = "", newGrade = "", stage = "", status = "", retYr = "";
                long courseRegId = 0, feeTid = 0; int retSem = 0; decimal retakeFee = 0; bool hasNewTotal = false;
                // Original snapshot — used to RESTORE the re-opened course record + the official
                // result on reversal.
                string origAcadYr = "", origGrade = ""; int origSem = 0; bool origWasPublished = false;
                long origResultId = 0;
                object origCw = DBNull.Value, origExam = DBNull.Value, origTotal = DBNull.Value, origGradept = DBNull.Value;
                using (var cmd = new MySqlCommand(
                    @"SELECT rr.regno, rr.courseID, rr.course_reg_id, rr.fee_tid, rr.fee_billed,
                             rr.retake_fee, rr.status, rr.new_grade, rr.new_total,
                             rr.retake_acad_year, rr.retake_semester,
                             rr.orig_acad_year, rr.orig_semester, rr.orig_course_work, rr.orig_exam,
                             rr.orig_total, rr.orig_grade, rr.orig_gradept, rr.orig_result_id,
                             COALESCE(cr.mark_stage,'NOT_ENTERED') AS stage
                      FROM campus_dynamics_portal.acad_retake_registrations rr
                      LEFT JOIN campus_dynamics_portal.acad_course_registration cr ON cr.ID = rr.course_reg_id
                      WHERE rr.ID = @id LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (!rd.Read()) return js.Serialize(new { ok = false, message = "Retake not found — it may have already been reversed." });
                        regno = (rd["regno"] ?? "").ToString();
                        courseID = (rd["courseID"] ?? "").ToString();
                        courseRegId = rd["course_reg_id"] == DBNull.Value ? 0 : Convert.ToInt64(rd["course_reg_id"]);
                        feeTid = rd["fee_tid"] == DBNull.Value ? 0 : Convert.ToInt64(rd["fee_tid"]);
                        feeBilled = (rd["fee_billed"] ?? "").ToString();
                        retakeFee = rd["retake_fee"] == DBNull.Value ? 0 : Convert.ToDecimal(rd["retake_fee"]);
                        status = (rd["status"] ?? "").ToString().Trim().ToUpperInvariant();
                        newGrade = (rd["new_grade"] ?? "").ToString().Trim();
                        hasNewTotal = rd["new_total"] != DBNull.Value;
                        retYr = (rd["retake_acad_year"] ?? "").ToString();
                        retSem = rd["retake_semester"] == DBNull.Value ? 0 : Convert.ToInt32(rd["retake_semester"]);
                        stage = (rd["stage"] ?? "NOT_ENTERED").ToString().Trim().ToUpperInvariant();
                        origAcadYr = (rd["orig_acad_year"] ?? "").ToString();
                        origSem = rd["orig_semester"] == DBNull.Value ? 0 : Convert.ToInt32(rd["orig_semester"]);
                        origCw = rd["orig_course_work"];
                        origExam = rd["orig_exam"];
                        origTotal = rd["orig_total"];
                        origGrade = (rd["orig_grade"] ?? "").ToString();
                        origGradept = rd["orig_gradept"];
                        origResultId = rd["orig_result_id"] == DBNull.Value ? 0 : Convert.ToInt64(rd["orig_result_id"]);
                        origWasPublished = origResultId > 0;
                    }
                }

                if (status == "COMPLETED" || stage == "PUBLISHED" || newGrade != "" || hasNewTotal)
                    return js.Serialize(new { ok = false, message = "This retake result is already PUBLISHED/completed — un-publish the marks first before reversing." });

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        string feeMsg = "no fee had been billed";
                        if (feeBilled == "Yes" && feeTid > 0 && retakeFee > 0)
                        {
                            // Item-21 Bill accumulates all retakes for the period: subtract this
                            // one, or delete the Bill if this was the only/last retake on it.
                            decimal curAmt = 0; bool haveBill = false;
                            using (var q = new MySqlCommand("SELECT amount FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TID=@t LIMIT 1", conn, tx))
                            { q.Parameters.AddWithValue("@t", feeTid); object o = q.ExecuteScalar(); if (o != null && o != DBNull.Value) { haveBill = true; curAmt = Convert.ToDecimal(o); } }
                            if (haveBill)
                            {
                                if (curAmt - retakeFee <= 0.5m)
                                {
                                    using (var d = new MySqlCommand("DELETE FROM campus_dynamics_accounts.fin_studentfeestracking WHERE TID=@t", conn, tx))
                                    { d.Parameters.AddWithValue("@t", feeTid); d.ExecuteNonQuery(); }
                                }
                                else
                                {
                                    using (var u = new MySqlCommand("UPDATE campus_dynamics_accounts.fin_studentfeestracking SET amount=amount-@f WHERE TID=@t", conn, tx))
                                    { u.Parameters.AddWithValue("@f", (double)retakeFee); u.Parameters.AddWithValue("@t", feeTid); u.ExecuteNonQuery(); }
                                }
                            }
                            // Remove this retake's GL pair (DR student + CR revenue), voucherNo = fee bill TID.
                            using (var dDr = new MySqlCommand(
                                @"DELETE FROM campus_dynamics_accounts.fin_ledger
                                  WHERE voucherNo=@v AND accountcode=@r AND account_type='Student'
                                    AND transactionType='DR' AND transaction_amount=@f ORDER BY TID DESC LIMIT 1", conn, tx))
                            { dDr.Parameters.AddWithValue("@v", feeTid); dDr.Parameters.AddWithValue("@r", regno); dDr.Parameters.AddWithValue("@f", (double)retakeFee); dDr.ExecuteNonQuery(); }
                            using (var dCr = new MySqlCommand(
                                @"DELETE FROM campus_dynamics_accounts.fin_ledger
                                  WHERE voucherNo=@v AND accountcode='AC6016' AND account_type='Chart Account'
                                    AND transactionType='CR' AND transaction_amount=@f ORDER BY TID DESC LIMIT 1", conn, tx))
                            { dCr.Parameters.AddWithValue("@v", feeTid); dCr.Parameters.AddWithValue("@f", (double)retakeFee); dCr.ExecuteNonQuery(); }
                            feeMsg = "UGX " + retakeFee.ToString("N0") + " retake fee reversed";
                        }

                        // Restore the re-opened course record to its ORIGINAL state (the retake
                        // reused this same row, so we must NOT delete it). Put back the original
                        // period, marks and stage from the snapshot, and clear the RT flags.
                        if (courseRegId > 0)
                        {
                            MarkAuditContext.Set(conn, tx, "RetakeController:rollback",
                                "Retake rolled back - original marks restored from the snapshot");
                            using (var d = new MySqlCommand(
                                @"UPDATE campus_dynamics_portal.acad_course_registration SET
                                    acad_year=@ay, semester=@sem, course_status='REGULAR', registration_type='NORMAL',
                                    retake_registration_id=NULL,
                                    provisional_course_work_marks=@cw, provisional_exam_marks=@ex, provisional_total_marks=@tot,
                                    provisional_marks_status=@pms, mark_stage=@stg
                                  WHERE ID=@c AND registration_type='RT'", conn, tx))
                            {
                                d.Parameters.AddWithValue("@ay", string.IsNullOrEmpty(origAcadYr) ? retYr : origAcadYr);
                                d.Parameters.AddWithValue("@sem", origSem > 0 ? origSem : retSem);
                                d.Parameters.AddWithValue("@cw", origCw);
                                d.Parameters.AddWithValue("@ex", origExam);
                                d.Parameters.AddWithValue("@tot", origTotal);
                                d.Parameters.AddWithValue("@pms", origWasPublished ? "published" : "not_entered");
                                d.Parameters.AddWithValue("@stg", origWasPublished ? "PUBLISHED" : "NOT_ENTERED");
                                d.Parameters.AddWithValue("@c", courseRegId);
                                d.ExecuteNonQuery();
                            }
                        }

                        // Restore the official published result that was blanked at registration
                        // (the retake "moved" the mark here). Put back score/grade/gradept from the
                        // snapshot and clear the is_retake flag.
                        if (origWasPublished && origResultId > 0)
                        {
                            using (var rr = new MySqlCommand(
                                "UPDATE campus_dynamics.acad_results SET score=@sc, grade=@g, gradept=@gp, is_retake=0 WHERE ID=@rid", conn, tx))
                            {
                                rr.Parameters.AddWithValue("@sc", origTotal);
                                rr.Parameters.AddWithValue("@g", string.IsNullOrEmpty(origGrade) ? (object)DBNull.Value : origGrade);
                                rr.Parameters.AddWithValue("@gp", origGradept);
                                rr.Parameters.AddWithValue("@rid", origResultId);
                                rr.ExecuteNonQuery();
                            }
                        }

                        using (var d = new MySqlCommand("DELETE FROM campus_dynamics_portal.acad_retake_registrations WHERE ID=@id", conn, tx))
                        { d.Parameters.AddWithValue("@id", id); d.ExecuteNonQuery(); }

                        using (var log = new MySqlCommand(
                            @"INSERT INTO campus_dynamics.acad_activity_log (user_id, page_function, par, comments, access_date)
                              VALUES (@u, 'Retake Reversal', @par, @c, NOW())", conn, tx))
                        {
                            log.Parameters.AddWithValue("@u", actor);
                            log.Parameters.AddWithValue("@par", regno + " | " + courseID + " | " + retYr + " Sem " + retSem);
                            string rsn = string.IsNullOrEmpty(reason) ? "" : " Reason: " + reason.Trim();
                            log.Parameters.AddWithValue("@c", "Retake reversed/deleted (" + feeMsg + ")." + rsn);
                            log.ExecuteNonQuery();
                        }

                        tx.Commit();
                        return js.Serialize(new { ok = true, message = "Retake for " + courseID + " reversed — " + feeMsg + ", original course registration restored." });
                    }
                    catch (Exception exTx)
                    {
                        try { tx.Rollback(); } catch { }
                        return js.Serialize(new { ok = false, message = "Reversal failed — no changes were saved: " + exTx.Message });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return js.Serialize(new { ok = false, message = ex.Message });
        }
    }

    // template helpers
    protected string StageBadge(object stage)
    {
        string s = (stage == null ? "" : stage.ToString()).Trim().ToUpperInvariant();
        string color = "#475569", bg = "#eef2f7";
        if (s == "PUBLISHED") { color = "#15803d"; bg = "#e6f4ea"; }
        else if (s == "APPROVED") { color = "#1d4ed8"; bg = "#e8f0fe"; }
        else if (s == "CAPTURED") { color = "#b45309"; bg = "#fff3e0"; }
        else if (s == "ENTERED") { color = "#7c3aed"; bg = "#f3e8ff"; }
        return string.Format("<span style='display:inline-block;padding:2px 9px;font-size:9.5px;font-weight:700;border-radius:10px;color:{0};background:{1};'>{2}</span>", color, bg, Server.HtmlEncode(s.Replace("_", " ")));
    }
    protected string StatusBadge(object status)
    {
        string s = (status == null ? "" : status.ToString()).Trim().ToUpperInvariant();
        string color = "#b45309", bg = "#fff3cd";
        if (s == "COMPLETED") { color = "#15803d"; bg = "#e6f4ea"; }
        else if (s == "CANCELLED") { color = "#b91c1c"; bg = "#fde8e8"; }
        return string.Format("<span style='display:inline-block;padding:2px 9px;font-size:9.5px;font-weight:700;border-radius:10px;color:{0};background:{1};'>{2}</span>", color, bg, Server.HtmlEncode(s));
    }

    private static int SafeInt(string v, int d) { int r; return int.TryParse(v, out r) ? r : d; }
    private static string N(object v) { if (v == null || v == DBNull.Value) return "0"; long n; return long.TryParse(v.ToString(), out n) ? n.ToString("N0") : v.ToString(); }
    private string C(object v)
    {
        string s = v == null || v == DBNull.Value ? "" : v.ToString();
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n")) return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
