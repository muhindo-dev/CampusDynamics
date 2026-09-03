using System;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Web.Script.Serialization;
using MySql.Data.MySqlClient;

public partial class API_v2_finance : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (ApiHelper.HandleCors(Request, Response)) return;

        string action = ApiHelper.Param(Request, "action", "").ToLower();

        if (ApiHelper.IsRateLimited(Request, Response, action)) return;

        try
        {
            switch (action)
            {
                case "ledger":                      HandleLedger();                   break;
                case "balance":                     HandleBalance();                  break;
                case "fees_structure":              HandleFeesStructure();            break;
                case "payment_history":             HandlePaymentHistory();           break;
                case "billing_summary":             HandleBillingSummary();           break;
                case "billing_breakdown":           HandleBillingBreakdown();         break;
                case "fee_status":                  HandleFeeStatus();                break;
                case "bulk_fee_check":              HandleBulkFeeCheck();             break;
                case "access_status":               HandleAccessStatus();             break;
                case "waivers":                     HandleWaivers();                  break;
                case "accommodation_status":        HandleAccommodationStatus();      break;
                case "student_financial_summary":   HandleStudentFinancialSummary();  break;
                // ── Chart of Accounts ──
                case "chart_of_accounts":   HandleChartOfAccounts();  break;
                case "account":             HandleAccount();          break;
                case "create_account":      HandleCreateAccount();    break;
                case "update_account":      HandleUpdateAccount();    break;
                case "delete_account":      HandleDeleteAccount();    break;
                // ── Residence ──
                case "residence_info":          HandleResidenceInfo();         break;
                case "halls":                   HandleHalls();                 break;
                case "allocate_residence":      HandleAllocateResidence();     break;
                case "remove_residence":        HandleRemoveResidence();       break;
                case "unallocated_residents":   HandleUnallocatedResidents();  break;
                case "hall_utilization":        HandleHallUtilization();       break;
                case "residence_fees":          HandleResidenceFees();         break;
                case "residence_ledger":        HandleResidenceLedger();       break;
                case "residence_report":        HandleResidenceReport();       break;
                default:
                    ApiHelper.Error(Response,
                        "Unknown action: " + action + ". Valid actions: ledger, balance, fees_structure, " +
                        "payment_history, billing_summary, billing_breakdown, fee_status, bulk_fee_check, " +
                        "access_status, waivers, accommodation_status, student_financial_summary, " +
                        "chart_of_accounts, account, create_account, update_account, delete_account, " +
                        "residence_info, halls, allocate_residence, remove_residence, unallocated_residents, " +
                        "hall_utilization, residence_fees, residence_ledger, residence_report",
                        "INVALID_ACTION");
                    break;
            }
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Internal server error: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUTH HELPER
    // ═══════════════════════════════════════════════════════════════════

    private string GetStudentRegNo(out TokenInfo auth)
    {
        auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return null;

        string regno = auth.UserType == "staff"
            ? ApiHelper.Param(Request, "regno", "")
            : auth.UserId;

        if (string.IsNullOrEmpty(regno))
        {
            ApiHelper.Error(Response, "Student registration number required. Pass ?regno= parameter.", "MISSING_PARAM");
            return null;
        }

        return regno;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LEDGER  — paginated chronological view
    // ═══════════════════════════════════════════════════════════════════

    private void HandleLedger()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        int page  = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int limit = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "limit", 50)));

        try
        {
            DataTable dt = FinanceEngine.GetDualLedger(regno);
            FinancialSummary totals = FinanceEngine.SummariseLedger(dt);

            int totalCount = dt.Rows.Count;
            int totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / limit);
            int offset     = (page - 1) * limit;

            // Build running balance for all rows before the current page
            decimal runningBalance = 0;
            for (int i = 0; i < Math.Min(offset, totalCount); i++)
            {
                DataRow row = dt.Rows[i];
                decimal d = 0, c = 0;
                if (row["debit"]  != DBNull.Value) decimal.TryParse(row["debit"].ToString(),  out d);
                if (row["credit"] != DBNull.Value) decimal.TryParse(row["credit"].ToString(), out c);
                runningBalance += d - c;
            }

            var entries = new List<Dictionary<string, object>>();
            for (int i = offset; i < Math.Min(offset + limit, totalCount); i++)
            {
                DataRow row = dt.Rows[i];
                decimal d = 0, c = 0;
                if (row["debit"]  != DBNull.Value) decimal.TryParse(row["debit"].ToString(),  out d);
                if (row["credit"] != DBNull.Value) decimal.TryParse(row["credit"].ToString(), out c);
                runningBalance += d - c;

                entries.Add(new Dictionary<string, object>
                {
                    { "trans_date",   row["trans_date"] != DBNull.Value ? Convert.ToDateTime(row["trans_date"]).ToString("yyyy-MM-dd") : null },
                    { "debit",        d                              },
                    { "credit",       c                              },
                    { "balance",      runningBalance                 },
                    { "detail",       row["detail"].ToString()       },
                    { "acad_year",    row["acad_year"].ToString()    },
                    { "semester",     row["semester"].ToString()     },
                    { "entry_source", row["entry_source"].ToString() }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "balance",        totals.Balance        },
                { "total_charges",  totals.TotalCharges   },
                { "total_payments", totals.TotalPayments  },
                { "currency",       FinanceEngine.CURRENCY },
                { "total_entries",  totalCount            },
                { "page",           page                  },
                { "total_pages",    totalPages            },
                { "limit",          limit                 },
                { "entries",        entries               }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching ledger: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BALANCE  — current all-time balance via FinanceEngine
    // ═══════════════════════════════════════════════════════════════════

    private void HandleBalance()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            FinancialSummary summary = FinanceEngine.ComputePeriodBalance(regno);
            ApiHelper.Success(Response, summary.ToDictionary());
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching balance: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FEES STRUCTURE
    // ═══════════════════════════════════════════════════════════════════

    private void HandleFeesStructure()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            // Student profile — progid, study year, nationality
            DataTable studentDt = ApiHelper.Query(
                @"SELECT s.progid, s.nationality,
                         GREATEST(1, LEAST(3, COALESCE(
                             (SELECT MAX(r.studyyear) FROM acad_registration r WHERE r.regno = s.regno), 1
                         ))) AS study_year
                  FROM acad_student s WHERE s.regno = @reg LIMIT 1",
                new MySqlParameter("@reg", regno)
            );

            if (studentDt.Rows.Count == 0) { ApiHelper.Error(Response, "Student not found.", "NOT_FOUND"); return; }

            DataRow sRow       = studentDt.Rows[0];
            string  progcode   = sRow["progid"].ToString();
            int     studyYear  = Convert.ToInt32(sRow["study_year"]);
            string  nationality = sRow["nationality"] != DBNull.Value ? sRow["nationality"].ToString() : "";
            string  feeCategory = nationality.ToLower().Contains("ugand") ? "Ugandan" : "International";

            decimal totalFees = 0;
            var items = new List<Dictionary<string, object>>();
            string feesSource = "not_found";

            // ── Source 1: actual billed items (most accurate — set after fin_Autobilling runs) ──
            DataTable billedDt = ApiHelper.QueryAccounts(
                @"SELECT t.acadyear, t.semester, t.item_code,
                         COALESCE(b.ItemName, CONCAT('Item ', t.item_code)) AS item_name,
                         SUM(t.amount) AS amount
                  FROM fin_studentfeestracking t
                  LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code
                  WHERE t.regno = @reg AND t.trans_type = 'Bill'
                  GROUP BY t.acadyear, t.semester, t.item_code
                  ORDER BY t.acadyear, t.semester, t.item_code",
                new MySqlParameter("@reg", regno)
            );

            if (billedDt.Rows.Count > 0)
            {
                feesSource = "billed";
                foreach (DataRow row in billedDt.Rows)
                {
                    decimal amt = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                    totalFees += amt;
                    items.Add(new Dictionary<string, object>
                    {
                        { "item",      row["item_code"].ToString()  },
                        { "item_name", row["item_name"].ToString()  },
                        { "amount",    amt                          },
                        { "semester",  row["semester"] != DBNull.Value ? row["semester"].ToString() : "" },
                        { "acad_year", row["acadyear"]  != DBNull.Value ? row["acadyear"].ToString()  : "" }
                    });
                }
            }
            else
            {
                // ── Source 2: defined fee structure from fin_programme_fees (wide format) ──
                // is_active stores 'Yes'/'No'; columns are y{year}_s{sem}_tuition / _functional
                string yr = studyYear.ToString();
                DataTable pfDt = ApiHelper.QueryAccounts(
                    "SELECT y" + yr + "_s1_tuition AS s1_tui, y" + yr + "_s1_functional AS s1_fun," +
                    "       y" + yr + "_s2_tuition AS s2_tui, y" + yr + "_s2_functional AS s2_fun," +
                    "       y" + yr + "_s3_tuition AS s3_tui, y" + yr + "_s3_functional AS s3_fun" +
                    // Resolved by the student's own study session: a programme may hold both a
                    // MAIN and an INSERVICE structure, and "WHERE progcode = X LIMIT 1" would
                    // pick an arbitrary one — quoting a day student in-service fees, or vice
                    // versa, with nothing to show it had happened. fin_ProgFeeRow applies the
                    // precedence (own session, else MAIN) in one place.
                    " FROM fin_programme_fees" +
                    " WHERE ID = fin_ProgFeeRow(@prog, (SELECT s.studsesion" +
                    "                                   FROM campus_dynamics.acad_student s" +
                    "                                   WHERE s.regno = @reg LIMIT 1))",
                    new MySqlParameter("@prog", progcode),
                    new MySqlParameter("@reg", regno)
                );

                if (pfDt.Rows.Count > 0)
                {
                    feesSource = "programme_fees";
                    DataRow r = pfDt.Rows[0];
                    for (int sem = 1; sem <= 3; sem++)
                    {
                        string sk = "s" + sem;
                        decimal tui = FsDecimal(r, sk + "_tui");
                        decimal fun = FsDecimal(r, sk + "_fun");
                        if (tui > 0)
                        {
                            totalFees += tui;
                            items.Add(new Dictionary<string, object>
                            {
                                { "item",      "TUITION_S" + sem          },
                                { "item_name", "Tuition Fees"             },
                                { "amount",    tui                        },
                                { "semester",  sem.ToString()             },
                                { "acad_year", ""                         }
                            });
                        }
                        if (fun > 0)
                        {
                            totalFees += fun;
                            items.Add(new Dictionary<string, object>
                            {
                                { "item",      "FUNCTIONAL_S" + sem       },
                                { "item_name", "Functional Fees"          },
                                { "amount",    fun                        },
                                { "semester",  sem.ToString()             },
                                { "acad_year", ""                         }
                            });
                        }
                    }
                }
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "programme_code", progcode   },
                { "study_year",     studyYear  },
                { "fee_category",   feeCategory },
                { "currency",       FinanceEngine.CURRENCY },
                { "total_fees",     totalFees  },
                { "fees_source",    feesSource },
                { "items",          items      }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching fees structure: " + ex.Message, "SERVER_ERROR");
        }
    }

    private static decimal FsDecimal(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return 0m;
        decimal v;
        return decimal.TryParse(row[col].ToString(), out v) ? v : 0m;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PAYMENT HISTORY
    // ═══════════════════════════════════════════════════════════════════

    private void HandlePaymentHistory()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DataTable dt = FinanceEngine.GetDualLedger(regno);

            var payments = new List<Dictionary<string, object>>();
            decimal totalPayments = 0;

            foreach (DataRow row in dt.Rows)
            {
                decimal credit = 0;
                if (row["credit"] != DBNull.Value) decimal.TryParse(row["credit"].ToString(), out credit);
                if (credit <= 0) continue;

                totalPayments += credit;
                payments.Add(new Dictionary<string, object>
                {
                    { "date",         row["trans_date"] != DBNull.Value ? Convert.ToDateTime(row["trans_date"]).ToString("yyyy-MM-dd") : null },
                    { "amount",       credit },
                    { "detail",       row["detail"].ToString()      },
                    { "acad_year",    row["acad_year"].ToString()   },
                    { "semester",     row["semester"].ToString()    },
                    { "entry_source", row["entry_source"].ToString() }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "total_payments", totalPayments  },
                { "payment_count",  payments.Count },
                { "currency",       FinanceEngine.CURRENCY },
                { "payments",       payments       }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching payment history: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BILLING SUMMARY
    // ═══════════════════════════════════════════════════════════════════

    private void HandleBillingSummary()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DataTable dt = FinanceEngine.GetDualLedger(regno);
            decimal grandCharges = 0, grandPayments = 0;

            var periodMap = new Dictionary<string, decimal[]>(); // key → [charges, payments]

            foreach (DataRow row in dt.Rows)
            {
                decimal debit = 0, credit = 0;
                if (row["debit"]  != DBNull.Value) decimal.TryParse(row["debit"].ToString(),  out debit);
                if (row["credit"] != DBNull.Value) decimal.TryParse(row["credit"].ToString(), out credit);
                grandCharges  += debit;
                grandPayments += credit;

                string ay  = row["acad_year"].ToString();
                string sem = row["semester"].ToString();
                string key = string.IsNullOrEmpty(ay) ? "general"
                           : (string.IsNullOrEmpty(sem) ? ay : ay + "_S" + sem);

                if (!periodMap.ContainsKey(key)) periodMap[key] = new decimal[] { 0, 0 };
                periodMap[key][0] += debit;
                periodMap[key][1] += credit;
            }

            var periods = new List<Dictionary<string, object>>();
            foreach (var kv in periodMap)
            {
                periods.Add(new Dictionary<string, object>
                {
                    { "period",   kv.Key          },
                    { "charges",  kv.Value[0]     },
                    { "payments", kv.Value[1]     },
                    { "balance",  kv.Value[0] - kv.Value[1] }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "overall_charges",  grandCharges              },
                { "overall_payments", grandPayments             },
                { "overall_balance",  grandCharges - grandPayments },
                { "currency",         FinanceEngine.CURRENCY    },
                { "periods",          periods                   }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching billing summary: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BILLING BREAKDOWN  — per-semester with waivers
    // ═══════════════════════════════════════════════════════════════════

    private void HandleBillingBreakdown()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DataTable dt = FinanceEngine.GetDualLedger(regno);

            var periodMap = new Dictionary<string, Dictionary<string, object>>();

            foreach (DataRow row in dt.Rows)
            {
                decimal debit = 0, credit = 0;
                if (row["debit"]  != DBNull.Value) decimal.TryParse(row["debit"].ToString(),  out debit);
                if (row["credit"] != DBNull.Value) decimal.TryParse(row["credit"].ToString(), out credit);

                string ay  = row["acad_year"].ToString();
                string sem = row["semester"].ToString();
                string key = string.IsNullOrEmpty(ay) ? "general"
                           : (string.IsNullOrEmpty(sem) ? ay + "_S0" : ay + "_S" + sem);

                if (!periodMap.ContainsKey(key))
                {
                    periodMap[key] = new Dictionary<string, object>
                    {
                        { "acad_year",     ay  }, { "semester",     sem },
                        { "total_billed",  0m  }, { "total_paid",   0m  },
                        { "total_waived",  0m  }, { "net_balance",  0m  }
                    };
                }

                periodMap[key]["total_billed"] = (decimal)periodMap[key]["total_billed"] + debit;
                periodMap[key]["total_paid"]   = (decimal)periodMap[key]["total_paid"]   + credit;
            }

            // Merge waiver amounts per period
            try
            {
                DataTable wDt = ApiHelper.QueryAccounts(
                    "SELECT w.acadyear AS acad_year, w.semester, COALESCE(SUM(wi.waived_amount), 0) AS waived " +
                    "FROM fin_bill_waiver_items wi " +
                    "INNER JOIN fin_bill_waivers w ON w.waiver_id = wi.waiver_id " +
                    "WHERE w.regno = @reg AND w.status = 'Active' " +
                    "GROUP BY w.acadyear, w.semester",
                    new MySqlParameter("@reg", regno)
                );

                foreach (DataRow wr in wDt.Rows)
                {
                    string ay  = wr["acad_year"] != DBNull.Value ? wr["acad_year"].ToString() : "";
                    string sem = wr["semester"]  != DBNull.Value ? wr["semester"].ToString()  : "";
                    string key = string.IsNullOrEmpty(ay) ? "general"
                               : (string.IsNullOrEmpty(sem) ? ay + "_S0" : ay + "_S" + sem);

                    decimal waived = 0;
                    if (wr["waived"] != DBNull.Value) decimal.TryParse(wr["waived"].ToString(), out waived);

                    if (!periodMap.ContainsKey(key))
                        periodMap[key] = new Dictionary<string, object>
                        {
                            { "acad_year", ay }, { "semester", sem },
                            { "total_billed", 0m }, { "total_paid", 0m },
                            { "total_waived", 0m }, { "net_balance", 0m }
                        };

                    periodMap[key]["total_waived"] = (decimal)periodMap[key]["total_waived"] + waived;
                }
            }
            catch { /* fin_bill_waivers may not exist yet */ }

            var breakdown = new List<Dictionary<string, object>>();
            decimal grandBilled = 0, grandPaid = 0, grandWaived = 0;

            foreach (var kv in periodMap)
            {
                decimal billed = (decimal)kv.Value["total_billed"];
                decimal paid   = (decimal)kv.Value["total_paid"];
                decimal waived = (decimal)kv.Value["total_waived"];
                decimal net    = billed - paid - waived;
                kv.Value["net_balance"] = net;
                breakdown.Add(kv.Value);
                grandBilled += billed; grandPaid += paid; grandWaived += waived;
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "grand_total_billed",  grandBilled                          },
                { "grand_total_paid",    grandPaid                            },
                { "grand_total_waived",  grandWaived                          },
                { "grand_net_balance",   grandBilled - grandPaid - grandWaived },
                { "currency",            FinanceEngine.CURRENCY               },
                { "breakdown",           breakdown                             }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching billing breakdown: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FEE STATUS  — period-specific, uses FinanceEngine for computation
    // ═══════════════════════════════════════════════════════════════════

    private void HandleFeeStatus()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        string semester = ApiHelper.Param(Request, "semester", "");

        if (!string.IsNullOrEmpty(acadYear) && !ApiHelper.ValidateAcadYear(acadYear, Response)) return;

        try
        {
            FinancialSummary summary = FinanceEngine.ComputePeriodBalance(regno, acadYear, semester);
            bool hasLock = FinanceEngine.HasFinancialLock(regno);

            var data = new Dictionary<string, object>
            {
                { "regno",              regno                    },
                { "fee_status",         summary.ClearanceStatus  },
                { "total_fees",         summary.TotalCharges     },
                { "amount_paid",        summary.TotalPayments    },
                { "balance",            summary.Balance          },
                { "currency",           FinanceEngine.CURRENCY   },
                { "last_payment_date",  summary.LastPaymentDate  },
                { "has_financial_lock", hasLock                  },
                { "academic_year",      acadYear                 },
                { "semester",           semester                 }
            };

            ApiHelper.Success(Response, data);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error checking fee status: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  BULK FEE CHECK  — staff only, uses FinanceEngine per student
    // ═══════════════════════════════════════════════════════════════════

    private void HandleBulkFeeCheck()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Only staff can perform bulk fee checks.", "ACCESS_DENIED"); return; }

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        if (!string.IsNullOrEmpty(acadYear) && !ApiHelper.ValidateAcadYear(acadYear, Response)) return;

        string body = "";
        try { using (var r = new System.IO.StreamReader(Request.InputStream)) body = r.ReadToEnd(); } catch { }

        var studentIds = new List<string>();
        if (!string.IsNullOrEmpty(body))
        {
            try
            {
                var parsed = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                if (parsed != null && parsed.ContainsKey("students"))
                {
                    var arr = parsed["students"] as System.Collections.ArrayList;
                    if (arr != null) foreach (object item in arr) studentIds.Add(Convert.ToString(item));
                }
            }
            catch { }
        }
        if (studentIds.Count == 0)
        {
            string sp = ApiHelper.Param(Request, "students", "");
            if (!string.IsNullOrEmpty(sp))
                foreach (string s in sp.Split(','))
                    if (!string.IsNullOrEmpty(s.Trim())) studentIds.Add(s.Trim());
        }
        if (studentIds.Count == 0)  { ApiHelper.Error(Response, "No student IDs provided.", "MISSING_PARAM"); return; }
        if (studentIds.Count > 200) { ApiHelper.Error(Response, "Maximum 200 students per request.", "VALIDATION_ERROR"); return; }

        var results = new List<Dictionary<string, object>>();
        foreach (string sid in studentIds)
        {
            try
            {
                FinancialSummary fs = FinanceEngine.ComputePeriodBalance(sid, acadYear);
                results.Add(new Dictionary<string, object>
                {
                    { "regno",       sid                   },
                    { "fee_status",  fs.ClearanceStatus    },
                    { "total_fees",  fs.TotalCharges       },
                    { "amount_paid", fs.TotalPayments      },
                    { "balance",     fs.Balance            }
                });
            }
            catch
            {
                results.Add(new Dictionary<string, object>
                {
                    { "regno", sid }, { "fee_status", "error" },
                    { "total_fees", 0 }, { "amount_paid", 0 }, { "balance", 0 }
                });
            }
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "academic_year",  acadYear       },
            { "total_checked",  results.Count  },
            { "currency",       FinanceEngine.CURRENCY },
            { "results",        results        }
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WAIVERS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleWaivers()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DataTable wDt = ApiHelper.QueryAccounts(
                "SELECT w.waiver_id, w.waiver_category, w.waiver_reason, w.total_amount, " +
                "  w.acadyear, w.semester, w.status, w.created_by, " +
                "  DATE_FORMAT(w.created_at, '%Y-%m-%d') AS created_at, " +
                "  w.reversed_by, DATE_FORMAT(w.reversed_at, '%Y-%m-%d') AS reversed_at, w.reversed_reason " +
                "FROM fin_bill_waivers w " +
                "WHERE w.regno = @reg " +
                "ORDER BY w.created_at DESC",
                new MySqlParameter("@reg", regno)
            );

            var waivers = new List<Dictionary<string, object>>();
            decimal totalWaived = 0;

            foreach (DataRow wr in wDt.Rows)
            {
                int waiverId = Convert.ToInt32(wr["waiver_id"]);
                decimal wTotal = 0;
                if (wr["total_amount"] != DBNull.Value) decimal.TryParse(wr["total_amount"].ToString(), out wTotal);

                DataTable items = ApiHelper.QueryAccounts(
                    "SELECT wi.item_id, wi.original_tid, wi.bill_amount, wi.waived_amount, wi.bill_detail " +
                    "FROM fin_bill_waiver_items wi WHERE wi.waiver_id = @wid ORDER BY wi.item_id",
                    new MySqlParameter("@wid", waiverId)
                );

                decimal itemWaived = 0;
                foreach (DataRow ir in items.Rows)
                {
                    decimal wa = 0;
                    if (ir["waived_amount"] != DBNull.Value) decimal.TryParse(ir["waived_amount"].ToString(), out wa);
                    itemWaived += wa;
                }
                if (wr["status"].ToString() == "Active") totalWaived += itemWaived;

                waivers.Add(new Dictionary<string, object>
                {
                    { "waiver_id",       waiverId                                                              },
                    { "category",        wr["waiver_category"].ToString()                                      },
                    { "reason",          wr["waiver_reason"] != DBNull.Value ? wr["waiver_reason"].ToString() : "" },
                    { "status",          wr["status"].ToString()                                               },
                    { "total_amount",    wTotal                                                                },
                    { "acad_year",       wr["acadyear"] != DBNull.Value ? wr["acadyear"].ToString() : ""       },
                    { "semester",        wr["semester"] != DBNull.Value ? wr["semester"].ToString() : ""       },
                    { "created_at",      wr["created_at"].ToString()                                           },
                    { "created_by",      wr["created_by"] != DBNull.Value ? wr["created_by"].ToString() : ""  },
                    { "reversed_by",     wr["reversed_by"]  != DBNull.Value ? wr["reversed_by"].ToString()  : "" },
                    { "reversed_at",     wr["reversed_at"]  != DBNull.Value ? wr["reversed_at"].ToString()  : "" },
                    { "reversed_reason", wr["reversed_reason"] != DBNull.Value ? wr["reversed_reason"].ToString() : "" },
                    { "items",           ApiHelper.TableToList(items)                                          }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "waivers",       waivers        },
                { "total_waivers", waivers.Count  },
                { "total_waived",  totalWaived    },
                { "currency",      FinanceEngine.CURRENCY }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching waivers: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ACCOMMODATION STATUS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleAccommodationStatus()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        int    semester = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            DataTable stDt = ApiHelper.Query(
                "SELECT s.StudentHall, " +
                "  COALESCE((SELECT r.residence_status FROM acad_registration r " +
                "             WHERE r.regno = s.regno ORDER BY r.ID DESC LIMIT 1), 'DAY') AS residence_status " +
                "FROM acad_student s WHERE s.regno = @reg",
                new MySqlParameter("@reg", regno)
            );

            if (stDt.Rows.Count == 0) { ApiHelper.Error(Response, "Student not found.", "NOT_FOUND"); return; }

            string residence = stDt.Rows[0]["residence_status"] != DBNull.Value ? stDt.Rows[0]["residence_status"].ToString() : "DAY";
            bool isResident  = (residence.ToUpper() == "RESIDENT" || residence.ToUpper() == "BOARDING");

            decimal billedAmount = 0;
            if (isResident && !string.IsNullOrEmpty(acadYear))
            {
                try
                {
                    DataTable acDt = ApiHelper.QueryAccounts(
                        "SELECT COALESCE(SUM(amount), 0) AS total " +
                        "FROM fin_studentfeestracking " +
                        "WHERE regno = @reg AND acadyear = @ay AND semester = @sem " +
                        "  AND trans_type = 'Bill' " +
                        "  AND (detail LIKE '%ccomo%' OR detail LIKE '%esidence%')",
                        new MySqlParameter("@reg", regno),
                        new MySqlParameter("@ay", acadYear),
                        new MySqlParameter("@sem", semester)
                    );
                    if (acDt.Rows.Count > 0) decimal.TryParse(acDt.Rows[0]["total"].ToString(), out billedAmount);
                }
                catch { }
            }

            string hostel = stDt.Rows[0]["StudentHall"] != DBNull.Value ? stDt.Rows[0]["StudentHall"].ToString() : "";

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "is_resident",        isResident                          },
                { "accommodation_type", isResident ? "On-Campus" : "Off-Campus" },
                { "residence_status",   residence                           },
                { "hostel",             hostel                              },
                { "billed_amount",      billedAmount                        },
                { "acad_year",          acadYear                            },
                { "semester",           semester                            },
                { "currency",           FinanceEngine.CURRENCY              }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching accommodation status: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  STUDENT FINANCIAL SUMMARY  — single-call comprehensive snapshot
    //  Combines all-time balance, period balance, waivers, and lock status.
    // ═══════════════════════════════════════════════════════════════════

    private void HandleStudentFinancialSummary()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        string semester = ApiHelper.Param(Request, "semester", "");

        if (!string.IsNullOrEmpty(acadYear) && !ApiHelper.ValidateAcadYear(acadYear, Response)) return;

        try
        {
            // All-time financial position
            FinancialSummary allTime = FinanceEngine.ComputePeriodBalance(regno);

            // Period-specific position (only when a period is specified)
            object periodData = null;
            if (!string.IsNullOrEmpty(acadYear))
            {
                FinancialSummary period = FinanceEngine.ComputePeriodBalance(regno, acadYear, semester);
                var pd = period.ToDictionary();
                pd["acad_year"] = acadYear;
                pd["semester"]  = semester;
                periodData = pd;
            }

            // Waivers
            decimal waiverTotal         = FinanceEngine.GetWaiverTotal(regno);
            decimal unpostedWaiverTotal = FinanceEngine.GetUnpostedWaiverTotal(regno);
            int     waiverCount         = FinanceEngine.GetWaiverCount(regno);

            // Financial lock
            bool hasLock = FinanceEngine.HasFinancialLock(regno);

            // Effective balance accounts only for UNPOSTED waivers because posted waiver
            // credits are already reflected in allTime.Balance via the ledger.
            decimal effectiveOwing  = allTime.AmountOwing > 0
                ? Math.Max(0, allTime.AmountOwing - unpostedWaiverTotal) : 0;
            decimal effectiveCredit = allTime.CreditBalance + unpostedWaiverTotal;

            var data = allTime.ToDictionary();
            data["regno"]              = regno;
            data["has_financial_lock"] = hasLock;
            data["waiver_total"]       = waiverTotal;
            data["waiver_count"]       = waiverCount;
            data["effective_balance"]  = effectiveOwing;
            data["credit_balance"]     = effectiveCredit;
            data["period"]             = periodData;

            ApiHelper.Success(Response, data);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching financial summary: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ACCESS STATUS  — fee access policy evaluation
    // ═══════════════════════════════════════════════════════════════════

    private void HandleAccessStatus()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DateTime evaluatedAt = DateTime.UtcNow;

            string studentName = "", programme = "", programmeCode = "";
            int studyYear = 0;
            try
            {
                DataTable dtStudent = ApiHelper.Query(
                    "SELECT CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) AS full_name, " +
                    "s.progid, COALESCE(p.progname,'') AS programme_name, " +
                    "COALESCE((SELECT MAX(r.studyyear) FROM acad_registration r WHERE r.regno = s.regno),1) AS study_year " +
                    "FROM acad_student s LEFT JOIN acad_programme p ON p.progID = s.progid " +
                    "WHERE s.regno = @reg",
                    new MySqlParameter("@reg", regno));
                if (dtStudent.Rows.Count > 0)
                {
                    studentName   = dtStudent.Rows[0]["full_name"].ToString().Trim();
                    programmeCode = dtStudent.Rows[0]["progid"] != DBNull.Value ? dtStudent.Rows[0]["progid"].ToString() : "";
                    programme     = dtStudent.Rows[0]["programme_name"].ToString();
                    int.TryParse(dtStudent.Rows[0]["study_year"].ToString(), out studyYear);
                }
            }
            catch { }

            var studentInfo = new Dictionary<string, object>
            {
                { "regno", regno }, { "name", studentName }, { "programme", programme },
                { "programme_code", programmeCode }, { "study_year", studyYear }
            };

            DataTable dtPolicy;
            try { dtPolicy = ApiHelper.QueryAccounts("SELECT * FROM fin_fee_access_policy WHERE is_active = 'yes' ORDER BY updated_at DESC, policy_id DESC LIMIT 1"); }
            catch { dtPolicy = new DataTable(); }

            if (dtPolicy.Rows.Count == 0)
            {
                ApiHelper.Success(Response, new Dictionary<string, object>
                {
                    { "access_allowed", true }, { "has_policy", false }, { "verdict", "granted" },
                    { "verdict_reason", "No active fee access policy. All students are granted full access." },
                    { "student", studentInfo },
                    { "policy", (object)null },
                    { "finance", new Dictionary<string, object> { { "total_bill", 0 }, { "total_paid", 0 }, { "balance", 0 }, { "percentage_paid", 0 }, { "currency", FinanceEngine.CURRENCY } } },
                    { "bursary", new Dictionary<string, object> { { "status", "None" }, { "scheme_name", "" }, { "amount_offered", 0 }, { "coverage_percent", 0 } } },
                    { "criteria", new List<Dictionary<string,object>> { new Dictionary<string,object> { { "rule","No Active Restriction" }, { "passed", true }, { "enabled", false }, { "detail","Fee access policy is currently disabled." }, { "threshold", (object)null } } } },
                    { "summary", new Dictionary<string,object> { { "total_rules", 0 }, { "rules_passed", 0 }, { "rules_failed", 0 }, { "enabled_rules", new List<string>() } } },
                    { "guidance", "No active fee access restrictions. All students are granted access." },
                    { "evaluated_at", evaluatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                });
                return;
            }

            DataRow pol = dtPolicy.Rows[0];
            string policyTitle = SafeStr(pol, "policy_title");
            string acadYear    = SafeStr(pol, "academic_year");
            int    semester    = SafeInt(pol, "semester");
            string logic       = SafeStr(pol, "rule_logic").ToUpper() == "ANY" ? "ANY" : "ALL";

            bool    balEnabled    = SafeStr(pol, "rule_min_balance_enabled") == "yes";
            decimal balMax        = SafeDec(pol, "rule_min_balance_amount");
            bool    winEnabled    = SafeStr(pol, "rule_payment_window_enabled") == "yes";
            decimal winMinAmt     = SafeDec(pol, "rule_payment_min_amount");
            DateTime? winStart    = SafeDate(pol, "rule_payment_window_start");
            DateTime? winEnd      = SafeDate(pol, "rule_payment_window_end");
            bool    pctEnabled    = SafeStr(pol, "rule_pct_paid_enabled") == "yes";
            decimal pctMin        = SafeDec(pol, "rule_pct_paid_minimum");
            bool    bursaryExempt = SafeStr(pol, "rule_bursary_exempt") == "yes";
            decimal bursaryMinCov = SafeDec(pol, "rule_bursary_min_coverage");
            bool    regRequired   = SafeStr(pol, "rule_require_registration") == "yes";

            var enabledRuleNames = new List<string>();
            if (balEnabled)    enabledRuleNames.Add("Balance Threshold");
            if (winEnabled)    enabledRuleNames.Add("Payment Window");
            if (pctEnabled)    enabledRuleNames.Add("Percentage Paid");
            if (bursaryExempt) enabledRuleNames.Add("Bursary Exemption");
            if (regRequired)   enabledRuleNames.Add("Registration");

            var policyConfig = new Dictionary<string, object>
            {
                { "policy_id", SafeInt(pol, "policy_id") }, { "title", policyTitle },
                { "academic_year", acadYear }, { "semester", semester },
                { "combination_logic", logic },
                { "combination_logic_description", logic == "ANY" ? "Student passes if ANY one enabled rule is satisfied" : "Student must satisfy ALL enabled rules to pass" },
                { "notes", SafeStr(pol, "policy_notes") },
                { "rules_enabled", new Dictionary<string,object> { { "balance_threshold", balEnabled }, { "payment_window", winEnabled }, { "percentage_paid", pctEnabled }, { "bursary_exemption", bursaryExempt }, { "registration", regRequired } } },
                { "thresholds", new Dictionary<string,object> { { "max_balance", balEnabled ? (object)balMax : null }, { "payment_window_min_amount", winEnabled ? (object)winMinAmt : null }, { "payment_window_start", winEnabled && winStart.HasValue ? (object)winStart.Value.ToString("yyyy-MM-dd") : null }, { "payment_window_end", winEnabled && winEnd.HasValue ? (object)winEnd.Value.ToString("yyyy-MM-dd") : null }, { "min_percentage_paid", pctEnabled ? (object)pctMin : null }, { "bursary_min_coverage", bursaryExempt ? (object)bursaryMinCov : null } } }
            };

            // Finance data via FinanceEngine — single canonical computation, no inline SQL
            FinancialSummary finSummary = FinanceEngine.ComputePeriodBalance(regno);
            decimal totalBill  = finSummary.TotalCharges;
            decimal totalPaid  = finSummary.TotalPayments;
            decimal balance2   = finSummary.Balance;
            decimal pctPaidAll = finSummary.PercentagePaid;

            var financeData = new Dictionary<string, object>
            {
                { "total_bill",     totalBill                },
                { "total_paid",     totalPaid                },
                { "balance",        -balance2                },
                { "amount_owing",   finSummary.AmountOwing   },
                { "credit_balance", finSummary.CreditBalance },
                { "percentage_paid",pctPaidAll               },
                { "currency",       FinanceEngine.CURRENCY   }
            };

            string bursaryStatus = "None", bursarySchemeName = "";
            decimal bursaryOffered = 0, bursaryCoverage = 0;
            var criteria = new List<Dictionary<string, object>>();
            bool bursaryShortCircuit = false;

            if (bursaryExempt)
            {
                DataTable dtBur = ApiHelper.QueryAccounts(
                    "SELECT ss.amount_offered, s.scholarshipName FROM scholarshipstudents ss " +
                    "JOIN scholarships s ON s.scholarshipID = ss.scholarshipID " +
                    "WHERE ss.adm_no = @reg AND ss.scholarhipYear = @ay AND ss.scholarhipTerm = @sem AND ss.status = 'Approved' LIMIT 1",
                    new MySqlParameter("@reg", regno), new MySqlParameter("@ay", acadYear), new MySqlParameter("@sem", semester));

                if (dtBur.Rows.Count > 0)
                {
                    bursaryOffered    = Convert.ToDecimal(dtBur.Rows[0]["amount_offered"]);
                    bursarySchemeName = dtBur.Rows[0]["scholarshipName"] != null ? dtBur.Rows[0]["scholarshipName"].ToString() : "Scholarship";
                    bursaryStatus     = "Active: " + bursarySchemeName;
                    bursaryCoverage   = totalBill > 0 ? Math.Round(bursaryOffered / totalBill * 100, 1) : 100;
                    bool bursaryPass  = bursaryCoverage >= bursaryMinCov;
                    if (bursaryPass) bursaryShortCircuit = true;
                    criteria.Add(new Dictionary<string, object> { { "rule", "Bursary Exemption" }, { "passed", bursaryPass }, { "enabled", true }, { "detail", bursaryPass ? string.Format("Bursary ({0}) with {1:F0}% coverage — exempt.", bursarySchemeName, bursaryCoverage) : string.Format("Bursary coverage {0:F0}% below required {1:F0}%.", bursaryCoverage, bursaryMinCov) }, { "threshold", string.Format("Min {0:F0}%", bursaryMinCov) }, { "actual_value", string.Format("{0:F0}%", bursaryCoverage) } });
                }
                else
                {
                    criteria.Add(new Dictionary<string, object> { { "rule", "Bursary Exemption" }, { "passed", false }, { "enabled", true }, { "detail", string.Format("No approved bursary for {0} Sem {1}.", acadYear, semester) }, { "threshold", string.Format("Min {0:F0}%", bursaryMinCov) }, { "actual_value", "No bursary" } });
                }
            }

            if (!bursaryShortCircuit)
            {
                if (balEnabled)
                {
                    bool pass = balance2 <= balMax;
                    criteria.Add(new Dictionary<string, object> { { "rule", "Balance Threshold" }, { "passed", pass }, { "enabled", true }, { "detail", pass ? string.Format("Balance {0:N0} within allowed {1:N0}.", balance2, balMax) : string.Format("Balance {0:N0} exceeds allowed {1:N0}.", balance2, balMax) }, { "threshold", string.Format("Max UGX {0:N0}", balMax) }, { "actual_value", string.Format("UGX {0:N0}", balance2) } });
                }
                if (winEnabled && winStart.HasValue && winEnd.HasValue)
                {
                    DataTable dtWin = ApiHelper.QueryAccounts(
                        "SELECT COALESCE(SUM(fl.transaction_amount), 0) AS window_payments FROM fin_ledger fl " +
                        "WHERE fl.accountcode = @reg AND fl.transactionType = 'CR' AND fl.transaction_amount > 0 " +
                        "  AND fl.transactionDate >= @ws AND fl.transactionDate <= @we",
                        new MySqlParameter("@reg", regno), new MySqlParameter("@ws", winStart.Value), new MySqlParameter("@we", winEnd.Value));
                    decimal wp = dtWin.Rows.Count > 0 ? Convert.ToDecimal(dtWin.Rows[0]["window_payments"]) : 0;
                    bool pass = wp >= winMinAmt;
                    criteria.Add(new Dictionary<string, object> { { "rule", "Payment Window" }, { "passed", pass }, { "enabled", true }, { "detail", pass ? string.Format("Paid {0:N0} in window (required {1:N0}).", wp, winMinAmt) : string.Format("Only paid {0:N0} in window (required {1:N0}).", wp, winMinAmt) }, { "threshold", string.Format("Min UGX {0:N0} between {1:yyyy-MM-dd} and {2:yyyy-MM-dd}", winMinAmt, winStart.Value, winEnd.Value) }, { "actual_value", string.Format("UGX {0:N0}", wp) } });
                }
                if (pctEnabled)
                {
                    decimal pct = totalBill > 0 ? Math.Round(totalPaid / totalBill * 100, 1) : 100;
                    bool pass = pct >= pctMin;
                    criteria.Add(new Dictionary<string, object> { { "rule", "Percentage Paid" }, { "passed", pass }, { "enabled", true }, { "detail", pass ? string.Format("{0:F1}% paid (required {1:F0}%).", pct, pctMin) : string.Format("Only {0:F1}% paid (required {1:F0}%).", pct, pctMin) }, { "threshold", string.Format("Min {0:F0}%", pctMin) }, { "actual_value", string.Format("{0:F1}%", pct) } });
                }
                if (regRequired)
                {
                    DataTable dtReg = ApiHelper.Query(
                        "SELECT regno FROM acad_registration WHERE regno = @reg AND acad_year = @ay AND semester = @sem AND regstatus = 'Registered' LIMIT 1",
                        new MySqlParameter("@reg", regno), new MySqlParameter("@ay", acadYear), new MySqlParameter("@sem", semester));
                    bool pass = dtReg.Rows.Count > 0;
                    criteria.Add(new Dictionary<string, object> { { "rule", "Registration" }, { "passed", pass }, { "enabled", true }, { "detail", pass ? string.Format("Registered for {0} Sem {1}.", acadYear, semester) : string.Format("NOT registered for {0} Sem {1}.", acadYear, semester) }, { "threshold", string.Format("Registered for {0} Sem {1}", acadYear, semester) }, { "actual_value", pass ? "Registered" : "Not registered" } });
                }
            }

            bool allowed;
            if      (bursaryShortCircuit) allowed = true;
            else if (criteria.Count == 0) allowed = true;
            else if (logic == "ANY")      { allowed = false; foreach (var cr in criteria) { if ((bool)cr["passed"]) { allowed = true; break; } } }
            else                          { allowed = true;  foreach (var cr in criteria) { if (!(bool)cr["passed"]) { allowed = false; break; } } }

            int rulesPassed = 0, rulesFailed = 0;
            foreach (var cr in criteria) { if ((bool)cr["passed"]) rulesPassed++; else rulesFailed++; }

            var tips = new List<string>();
            if (!allowed) foreach (var cr in criteria) { if (!(bool)cr["passed"]) {
                string rule = cr["rule"].ToString();
                if (rule == "Balance Threshold") tips.Add(string.Format("Pay at least UGX {0:N0} to reduce balance to the allowed maximum.", Math.Max(0, balance2 - balMax)));
                else if (rule == "Payment Window" && winEnd.HasValue) tips.Add(string.Format("Make a payment of at least UGX {0:N0} before {1:yyyy-MM-dd}.", winMinAmt, winEnd.Value));
                else if (rule == "Percentage Paid") { decimal need = pctMin / 100 * totalBill - totalPaid; if (need > 0) tips.Add(string.Format("Pay an additional UGX {0:N0} to reach {1:F0}%.", need, pctMin)); }
                else if (rule == "Registration") tips.Add(string.Format("Complete registration for {0} Semester {1}.", acadYear, semester));
            }}

            string verdictReason;
            if (bursaryShortCircuit)      verdictReason = string.Format("Exempt via bursary ({0}).", bursarySchemeName);
            else if (criteria.Count == 0) verdictReason = "No rules enabled — all students pass by default.";
            else if (allowed && logic == "ANY") verdictReason = string.Format("{0}/{1} rule(s) passed. ANY logic.", rulesPassed, criteria.Count);
            else if (allowed)             verdictReason = string.Format("All {0} rule(s) passed.", criteria.Count);
            else if (logic == "ANY")      verdictReason = string.Format("0/{0} rules passed. ANY logic.", criteria.Count);
            else                          verdictReason = string.Format("{0}/{1} rule(s) failed. ALL logic.", rulesFailed, criteria.Count);

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "access_allowed", allowed }, { "has_policy", true },
                { "verdict",        allowed ? "granted" : "denied" },
                { "verdict_reason", verdictReason },
                { "student",        studentInfo }, { "policy", policyConfig },
                { "finance",        financeData },
                { "bursary",        new Dictionary<string,object> { { "status", bursaryStatus }, { "scheme_name", bursarySchemeName }, { "amount_offered", bursaryOffered }, { "coverage_percent", bursaryCoverage }, { "exempt", bursaryShortCircuit } } },
                { "criteria",       criteria },
                { "summary",        new Dictionary<string,object> { { "total_rules", criteria.Count }, { "rules_passed", rulesPassed }, { "rules_failed", rulesFailed }, { "enabled_rules", enabledRuleNames } } },
                { "guidance",       string.Join(" ", tips.ToArray()) },
                { "evaluated_at",   evaluatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error evaluating access status: " + ex.Message, "ACCESS_STATUS_ERROR");
        }
    }

    // ─── Helpers for access_status ────────────────────────────────────────────

    private static string SafeStr(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == null || row[col] == DBNull.Value) return "";
        return row[col].ToString();
    }
    private static int SafeInt(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == null || row[col] == DBNull.Value) return 0;
        int v; return int.TryParse(row[col].ToString(), out v) ? v : 0;
    }
    private static decimal SafeDec(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == null || row[col] == DBNull.Value) return 0;
        decimal v; return decimal.TryParse(row[col].ToString(), out v) ? v : 0;
    }
    private static DateTime? SafeDate(DataRow row, string col)
    {
        if (!row.Table.Columns.Contains(col) || row[col] == null || row[col] == DBNull.Value) return null;
        DateTime v; if (DateTime.TryParse(row[col].ToString(), out v)) return v;
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CHART OF ACCOUNTS
    // ═══════════════════════════════════════════════════════════════════

    private void HandleChartOfAccounts()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string q      = ApiHelper.Param(Request, "q", "");
        string catFilter = ApiHelper.Param(Request, "category", "");
        int page      = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int size      = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));
        int offset    = (page - 1) * size;

        var where = new System.Text.StringBuilder("WHERE 1=1");
        var parms = new List<MySqlParameter>();

        if (!string.IsNullOrEmpty(q))
        {
            where.Append(" AND (ma.AccountCode LIKE @q OR ma.AccountName LIKE @q)");
            parms.Add(new MySqlParameter("@q", "%" + q + "%"));
        }
        if (!string.IsNullOrEmpty(catFilter))
        {
            where.Append(" AND ma.Category = @cat");
            parms.Add(new MySqlParameter("@cat", catFilter));
        }

        var countParms = new List<MySqlParameter>(parms);
        int total;
        try
        {
            total = Convert.ToInt32(ApiHelper.QueryAccounts(
                "SELECT COUNT(*) FROM fin_mainaccounts ma " + where, countParms.ToArray()).Rows[0][0]);
        }
        catch
        {
            total = 0;
        }

        parms.Add(new MySqlParameter("@lim", size));
        parms.Add(new MySqlParameter("@off", offset));

        DataTable dt;
        try
        {
            dt = ApiHelper.QueryAccounts(
                @"SELECT ma.ID AS account_id, ma.AccountCode AS account_code, ma.AccountName AS account_name,
                         ma.Category AS category, ma.SubCategory AS sub_category,
                         ma.AccountType AS account_type, ma.IsActive AS is_active,
                         ma.OpeningBalance AS opening_balance, ma.Description AS description
                  FROM fin_mainaccounts ma " + where + " ORDER BY ma.AccountCode LIMIT @lim OFFSET @off",
                parms.ToArray());
        }
        catch
        {
            ApiHelper.Error(Response, "Chart of accounts not accessible.", "SERVER_ERROR"); return;
        }

        ApiHelper.Success(Response, new Dictionary<string, object>
        {
            { "total", total }, { "page", page }, { "size", size },
            { "pages", (int)Math.Ceiling(total / (double)size) },
            { "accounts", ApiHelper.TableToList(dt) }
        });
    }

    private void HandleAccount()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int accountId   = ApiHelper.ParamInt(Request, "account_id", 0);
        string accCode  = ApiHelper.Param(Request, "account_code", "");

        if (accountId <= 0 && string.IsNullOrEmpty(accCode))
        {
            ApiHelper.Error(Response, "account_id or account_code is required.", "MISSING_PARAM"); return;
        }

        string cond = accountId > 0 ? "ID = @id" : "AccountCode = @id";
        object idVal = accountId > 0 ? (object)accountId : accCode;

        DataTable dt;
        try
        {
            dt = ApiHelper.QueryAccounts(
                @"SELECT ID AS account_id, AccountCode AS account_code, AccountName AS account_name,
                         Category AS category, SubCategory AS sub_category,
                         AccountType AS account_type, IsActive AS is_active,
                         OpeningBalance AS opening_balance, Description AS description
                  FROM fin_mainaccounts WHERE " + cond + " LIMIT 1",
                new MySqlParameter("@id", idVal));
        }
        catch
        {
            ApiHelper.Error(Response, "Account not accessible.", "SERVER_ERROR"); return;
        }

        if (dt.Rows.Count == 0) { ApiHelper.Error(Response, "Account not found.", "NOT_FOUND"); return; }

        ApiHelper.Success(Response, ApiHelper.FirstRowToDict(dt));
    }

    private void HandleCreateAccount()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string accCode  = ApiHelper.RequireParam(Request, Response, "account_code"); if (accCode == null) return;
        string accName  = ApiHelper.RequireParam(Request, Response, "account_name"); if (accName == null) return;
        string category = ApiHelper.Param(Request, "category", "");
        string subCat   = ApiHelper.Param(Request, "sub_category", "");
        string accType  = ApiHelper.Param(Request, "account_type", "");
        string desc     = ApiHelper.Param(Request, "description", "");
        decimal opening = 0;
        decimal.TryParse(ApiHelper.Param(Request, "opening_balance", "0"), out opening);

        try
        {
            ApiHelper.QueryAccounts(
                "CALL MainAccountEditor(@code, @name, @cat, @sub, @type, @desc, @ob, 'CREATE')",
                new MySqlParameter("@code", accCode),
                new MySqlParameter("@name", accName),
                new MySqlParameter("@cat",  category),
                new MySqlParameter("@sub",  subCat),
                new MySqlParameter("@type", accType),
                new MySqlParameter("@desc", desc),
                new MySqlParameter("@ob",   opening));
        }
        catch
        {
            // Fallback: direct INSERT if SP doesn't exist
            try
            {
                ApiHelper.QueryAccounts(
                    @"INSERT INTO fin_mainaccounts (AccountCode, AccountName, Category, SubCategory, AccountType, Description, OpeningBalance, IsActive)
                      VALUES (@code, @name, @cat, @sub, @type, @desc, @ob, 1)",
                    new MySqlParameter("@code", accCode),
                    new MySqlParameter("@name", accName),
                    new MySqlParameter("@cat",  category),
                    new MySqlParameter("@sub",  subCat),
                    new MySqlParameter("@type", accType),
                    new MySqlParameter("@desc", desc),
                    new MySqlParameter("@ob",   opening));
            }
            catch (Exception ex2)
            {
                ApiHelper.Error(Response, "Error creating account: " + ex2.Message, "SERVER_ERROR"); return;
            }
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "account_code", accCode } }, "Account created");
    }

    private void HandleUpdateAccount()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int accountId = ApiHelper.ParamInt(Request, "account_id", 0);
        if (accountId <= 0) { ApiHelper.Error(Response, "account_id is required.", "MISSING_PARAM"); return; }

        var sets = new List<string>();
        var parms = new List<MySqlParameter>();
        parms.Add(new MySqlParameter("@id", accountId));

        string accName  = ApiHelper.Param(Request, "account_name", "");
        string category = ApiHelper.Param(Request, "category", "");
        string subCat   = ApiHelper.Param(Request, "sub_category", "");
        string accType  = ApiHelper.Param(Request, "account_type", "");
        string desc     = ApiHelper.Param(Request, "description", "");
        string isActive = ApiHelper.Param(Request, "is_active", "");

        if (!string.IsNullOrEmpty(accName))  { sets.Add("AccountName = @n");    parms.Add(new MySqlParameter("@n",  accName));  }
        if (!string.IsNullOrEmpty(category)) { sets.Add("Category = @cat");     parms.Add(new MySqlParameter("@cat",category)); }
        if (!string.IsNullOrEmpty(subCat))   { sets.Add("SubCategory = @sub");  parms.Add(new MySqlParameter("@sub",subCat));   }
        if (!string.IsNullOrEmpty(accType))  { sets.Add("AccountType = @typ");  parms.Add(new MySqlParameter("@typ",accType));  }
        if (!string.IsNullOrEmpty(desc))     { sets.Add("Description = @d");    parms.Add(new MySqlParameter("@d",  desc));     }
        if (!string.IsNullOrEmpty(isActive)) { sets.Add("IsActive = @ia");      parms.Add(new MySqlParameter("@ia", isActive == "1" ? 1 : 0)); }

        if (sets.Count == 0) { ApiHelper.Error(Response, "No fields to update.", "VALIDATION_ERROR"); return; }

        try
        {
            ApiHelper.QueryAccounts(
                "UPDATE fin_mainaccounts SET " + string.Join(", ", sets) + " WHERE ID = @id",
                parms.ToArray());
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error updating account: " + ex.Message, "SERVER_ERROR"); return;
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "account_id", accountId } }, "Account updated");
    }

    private void HandleDeleteAccount()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        int accountId = ApiHelper.ParamInt(Request, "account_id", 0);
        if (accountId <= 0) { ApiHelper.Error(Response, "account_id is required.", "MISSING_PARAM"); return; }

        try
        {
            ApiHelper.QueryAccounts(
                "CALL DeleteMainAccount(@id)",
                new MySqlParameter("@id", accountId));
        }
        catch
        {
            // Fallback: direct DELETE
            try
            {
                ApiHelper.QueryAccounts(
                    "DELETE FROM fin_mainaccounts WHERE ID = @id",
                    new MySqlParameter("@id", accountId));
            }
            catch (Exception ex2)
            {
                ApiHelper.Error(Response, "Error deleting account: " + ex2.Message, "SERVER_ERROR"); return;
            }
        }

        ApiHelper.Success(Response, new Dictionary<string, object> { { "deleted_id", accountId } }, "Account deleted");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESIDENCE INFO  — student's hall allocation for a period
    // ═══════════════════════════════════════════════════════════════════

    private void HandleResidenceInfo()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        int    semester = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            // Latest registration for residence_status
            DataTable regDt = ApiHelper.Query(
                @"SELECT r.acad_year, r.semester, r.studyyear, r.residence_status,
                         CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) AS full_name,
                         s.StudentHall
                  FROM acad_registration r
                  JOIN acad_student s ON s.regno = r.regno
                  WHERE r.regno = @reg
                  ORDER BY r.ID DESC LIMIT 1",
                new MySqlParameter("@reg", regno)
            );

            if (regDt.Rows.Count == 0) { ApiHelper.Error(Response, "Student not found.", "NOT_FOUND"); return; }

            DataRow reg = regDt.Rows[0];
            string resStatus = reg["residence_status"] != DBNull.Value ? reg["residence_status"].ToString() : "DAY";
            string effectiveYear = !string.IsNullOrEmpty(acadYear) ? acadYear : reg["acad_year"].ToString();

            // Hall allocation
            DataTable hallDt = ApiHelper.Query(
                @"SELECT ar.ID AS allocation_id, ar.room_id,
                         h.hall_name, h.hall_capacity,
                         ar.acadyear, ar.semester
                  FROM acad_residence ar
                  JOIN acad_halls h ON h.ID = ar.hall_id
                  WHERE ar.regno = @reg AND ar.acadyear = @ay AND ar.semester = @sem
                  LIMIT 1",
                new MySqlParameter("@reg", regno),
                new MySqlParameter("@ay", effectiveYear),
                new MySqlParameter("@sem", semester)
            );

            bool allocated = hallDt.Rows.Count > 0;

            var result = new Dictionary<string, object>
            {
                { "regno",            regno                                  },
                { "full_name",        reg["full_name"].ToString().Trim()     },
                { "residence_status", resStatus                              },
                { "is_resident",      resStatus.ToUpper() == "RESIDENT" || resStatus.ToUpper() == "BOARDING" },
                { "is_allocated",     allocated                              },
                { "acad_year",        effectiveYear                          },
                { "semester",         semester                               },
                { "hall_name",        allocated ? hallDt.Rows[0]["hall_name"].ToString() : ""    },
                { "room_id",          allocated && hallDt.Rows[0]["room_id"] != DBNull.Value
                                          ? hallDt.Rows[0]["room_id"].ToString() : ""            },
                { "allocation_id",    allocated ? Convert.ToInt32(hallDt.Rows[0]["allocation_id"]) : 0 }
            };

            ApiHelper.Success(Response, result);
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching residence info: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HALLS  — list all halls with capacity (any authenticated user)
    // ═══════════════════════════════════════════════════════════════════

    private void HandleHalls()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        int    semester = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            string ay = !string.IsNullOrEmpty(acadYear) ? acadYear : DateTime.Now.Year + "/" + (DateTime.Now.Year + 1);

            DataTable dt = ApiHelper.Query(
                @"SELECT h.ID AS hall_id, h.hall_name, COALESCE(h.hall_capacity, 0) AS capacity,
                         COALESCE((SELECT COUNT(*) FROM acad_residence r
                                   WHERE r.hall_id = h.ID AND r.acadyear = @ay AND r.semester = @sem), 0) AS occupied
                  FROM acad_halls h
                  WHERE h.hall_name IS NOT NULL
                  ORDER BY h.hall_name",
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", semester)
            );

            var halls = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                int cap  = Convert.ToInt32(row["capacity"]);
                int occ  = Convert.ToInt32(row["occupied"]);
                halls.Add(new Dictionary<string, object>
                {
                    { "hall_id",   Convert.ToInt32(row["hall_id"])   },
                    { "hall_name", row["hall_name"].ToString()        },
                    { "capacity",  cap                                },
                    { "occupied",  occ                                },
                    { "available", Math.Max(0, cap - occ)            }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "acad_year", ay       },
                { "semester",  semester },
                { "halls",     halls    }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching halls: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ALLOCATE RESIDENCE  — staff: assign or update student hall/room
    // ═══════════════════════════════════════════════════════════════════

    private void HandleAllocateResidence()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string regno  = ApiHelper.RequireParam(Request, Response, "regno");   if (regno == null) return;
        int    hallId = ApiHelper.ParamInt(Request, "hall_id", 0);
        string roomId = ApiHelper.Param(Request, "room_id", "");
        string ay     = ApiHelper.RequireParam(Request, Response, "acad_year"); if (ay == null) return;
        int    sem    = ApiHelper.ParamInt(Request, "semester", 1);

        if (hallId <= 0) { ApiHelper.Error(Response, "hall_id is required.", "MISSING_PARAM"); return; }

        try
        {
            // Upsert: update if exists, insert otherwise
            DataTable existing = ApiHelper.Query(
                "SELECT ID FROM acad_residence WHERE regno = @reg AND acadyear = @ay AND semester = @sem LIMIT 1",
                new MySqlParameter("@reg", regno),
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", sem)
            );

            if (existing.Rows.Count > 0)
            {
                int existId = Convert.ToInt32(existing.Rows[0]["ID"]);
                ApiHelper.Execute(
                    "UPDATE acad_residence SET hall_id = @hid, room_id = @rid WHERE ID = @id",
                    new MySqlParameter("@hid", hallId),
                    new MySqlParameter("@rid", string.IsNullOrEmpty(roomId) ? (object)DBNull.Value : roomId),
                    new MySqlParameter("@id",  existId)
                );
                ApiHelper.Success(Response, new Dictionary<string, object>
                {
                    { "allocation_id", existId }, { "regno", regno },
                    { "hall_id", hallId }, { "room_id", roomId }, { "action", "updated" }
                }, "Residence updated");
            }
            else
            {
                ApiHelper.Execute(
                    "INSERT INTO acad_residence (regno, hall_id, room_id, acadyear, semester) VALUES (@reg, @hid, @rid, @ay, @sem)",
                    new MySqlParameter("@reg", regno),
                    new MySqlParameter("@hid", hallId),
                    new MySqlParameter("@rid", string.IsNullOrEmpty(roomId) ? (object)DBNull.Value : roomId),
                    new MySqlParameter("@ay",  ay),
                    new MySqlParameter("@sem", sem)
                );
                ApiHelper.Success(Response, new Dictionary<string, object>
                {
                    { "regno", regno }, { "hall_id", hallId },
                    { "room_id", roomId }, { "action", "created" }
                }, "Residence allocated");
            }
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error allocating residence: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  REMOVE RESIDENCE  — staff: remove student's hall allocation
    // ═══════════════════════════════════════════════════════════════════

    private void HandleRemoveResidence()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string regno = ApiHelper.RequireParam(Request, Response, "regno");    if (regno == null) return;
        string ay    = ApiHelper.RequireParam(Request, Response, "acad_year"); if (ay == null) return;
        int    sem   = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            int affected = ApiHelper.Execute(
                "DELETE FROM acad_residence WHERE regno = @reg AND acadyear = @ay AND semester = @sem",
                new MySqlParameter("@reg", regno),
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", sem)
            );

            if (affected == 0)
            {
                ApiHelper.Error(Response, "No allocation found for the given student and period.", "NOT_FOUND");
                return;
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "regno", regno }, { "acad_year", ay }, { "semester", sem }, { "rows_removed", affected }
            }, "Residence allocation removed");
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error removing residence: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  UNALLOCATED RESIDENTS  — staff: RESIDENT registrations with no hall
    // ═══════════════════════════════════════════════════════════════════

    private void HandleUnallocatedResidents()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string ay  = ApiHelper.RequireParam(Request, Response, "acad_year"); if (ay == null) return;
        int    sem = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            DataTable dt = ApiHelper.Query(
                @"SELECT r.regno,
                         CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) AS full_name,
                         s.progid, r.studyyear, r.residence_status, r.regstatus
                  FROM acad_registration r
                  JOIN acad_student s ON s.regno = r.regno
                  LEFT JOIN acad_residence ar ON ar.regno = r.regno AND ar.acadyear = @ay AND ar.semester = @sem
                  WHERE r.acad_year = @ay
                    AND r.residence_status = 'RESIDENT'
                    AND (ar.ID IS NULL OR ar.hall_id IS NULL OR ar.hall_id = 0)
                  ORDER BY s.firstname, s.othername",
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", sem)
            );

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "acad_year",  ay              },
                { "semester",   sem             },
                { "count",      dt.Rows.Count   },
                { "students",   ApiHelper.TableToList(dt) }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching unallocated residents: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HALL UTILIZATION  — staff: per-hall occupancy stats
    // ═══════════════════════════════════════════════════════════════════

    private void HandleHallUtilization()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string ay  = ApiHelper.RequireParam(Request, Response, "acad_year"); if (ay == null) return;
        int    sem = ApiHelper.ParamInt(Request, "semester", 1);

        try
        {
            DataTable dt = ApiHelper.Query(
                @"SELECT h.ID AS hall_id, h.hall_name,
                         COALESCE(h.hall_capacity, 0) AS capacity,
                         COUNT(ar.ID) AS occupied,
                         GREATEST(0, COALESCE(h.hall_capacity, 0) - COUNT(ar.ID)) AS available
                  FROM acad_halls h
                  LEFT JOIN acad_residence ar ON ar.hall_id = h.ID AND ar.acadyear = @ay AND ar.semester = @sem
                  WHERE h.hall_name IS NOT NULL
                  GROUP BY h.ID, h.hall_name, h.hall_capacity
                  ORDER BY h.hall_name",
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", sem)
            );

            int totalCap = 0, totalOcc = 0;
            var halls = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                int cap = Convert.ToInt32(row["capacity"]);
                int occ = Convert.ToInt32(row["occupied"]);
                totalCap += cap; totalOcc += occ;
                halls.Add(new Dictionary<string, object>
                {
                    { "hall_id",   Convert.ToInt32(row["hall_id"]) },
                    { "hall_name", row["hall_name"].ToString()      },
                    { "capacity",  cap                              },
                    { "occupied",  occ                              },
                    { "available", Convert.ToInt32(row["available"]) },
                    { "occupancy_pct", cap > 0 ? Math.Round((double)occ / cap * 100, 1) : 0.0 }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "acad_year",       ay                          },
                { "semester",        sem                         },
                { "total_capacity",  totalCap                    },
                { "total_occupied",  totalOcc                    },
                { "total_available", Math.Max(0, totalCap - totalOcc) },
                { "overall_pct",     totalCap > 0 ? Math.Round((double)totalOcc / totalCap * 100, 1) : 0.0 },
                { "halls",           halls                       }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching hall utilization: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESIDENCE FEES  — student's accommodation billing per semester
    // ═══════════════════════════════════════════════════════════════════

    private void HandleResidenceFees()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        string acadYear = ApiHelper.Param(Request, "acad_year", "");
        string semester = ApiHelper.Param(Request, "semester", "");

        try
        {
            var whereParts = new System.Text.StringBuilder(
                "WHERE t.regno = @reg AND t.trans_type = 'Bill'" +
                " AND (LOWER(COALESCE(t.detail,'')) LIKE '%ccomo%'" +
                "   OR LOWER(COALESCE(t.detail,'')) LIKE '%esidence%'" +
                "   OR LOWER(COALESCE(t.detail,'')) LIKE '%hostel%'" +
                "   OR LOWER(COALESCE(t.detail,'')) LIKE '%hall%')");

            var parms = new List<MySqlParameter> { new MySqlParameter("@reg", regno) };
            if (!string.IsNullOrEmpty(acadYear)) { whereParts.Append(" AND t.acadyear = @ay"); parms.Add(new MySqlParameter("@ay", acadYear)); }
            if (!string.IsNullOrEmpty(semester)) { whereParts.Append(" AND t.semester = @sem"); parms.Add(new MySqlParameter("@sem", semester)); }

            DataTable dt = ApiHelper.QueryAccounts(
                @"SELECT t.acadyear, t.semester, t.item_code, t.detail, t.amount,
                         DATE_FORMAT(t.trans_date, '%Y-%m-%d') AS trans_date,
                         COALESCE(b.ItemName, t.detail) AS item_name
                  FROM fin_studentfeestracking t
                  LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code
                  " + whereParts + " ORDER BY t.acadyear, t.semester, t.trans_date",
                parms.ToArray()
            );

            decimal total = 0;
            var items = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                decimal amt = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                total += amt;
                items.Add(new Dictionary<string, object>
                {
                    { "item_code",  row["item_code"] != DBNull.Value ? row["item_code"].ToString() : "" },
                    { "item_name",  row["item_name"].ToString()  },
                    { "amount",     amt                          },
                    { "acad_year",  row["acadyear"] != DBNull.Value ? row["acadyear"].ToString() : ""   },
                    { "semester",   row["semester"] != DBNull.Value ? row["semester"].ToString() : ""   },
                    { "trans_date", row["trans_date"].ToString() }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "regno",        regno              },
                { "total_billed", total              },
                { "currency",     FinanceEngine.CURRENCY },
                { "items",        items              }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching residence fees: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESIDENCE LEDGER  — all accommodation-related financial entries
    // ═══════════════════════════════════════════════════════════════════

    private void HandleResidenceLedger()
    {
        TokenInfo auth;
        string regno = GetStudentRegNo(out auth);
        if (regno == null) return;

        try
        {
            DataTable dt = ApiHelper.QueryAccounts(
                @"SELECT t.trans_type, t.acadyear, t.semester, t.item_code,
                         COALESCE(b.ItemName, t.detail) AS description,
                         t.amount, DATE_FORMAT(t.trans_date, '%Y-%m-%d') AS trans_date
                  FROM fin_studentfeestracking t
                  LEFT JOIN academicbillingitems b ON b.ItemCode = t.item_code
                  WHERE t.regno = @reg
                    AND (LOWER(COALESCE(t.detail,'')) LIKE '%ccomo%'
                      OR LOWER(COALESCE(t.detail,'')) LIKE '%esidence%'
                      OR LOWER(COALESCE(t.detail,'')) LIKE '%hostel%'
                      OR LOWER(COALESCE(t.detail,'')) LIKE '%hall%')
                  ORDER BY t.trans_date ASC, t.ID ASC",
                new MySqlParameter("@reg", regno)
            );

            decimal totalBilled  = 0, totalPaid = 0;
            var entries = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                decimal amt = row["amount"] != DBNull.Value ? Convert.ToDecimal(row["amount"]) : 0;
                string  ttype = row["trans_type"].ToString();
                if (ttype == "Bill")    totalBilled += amt;
                else if (ttype == "Pay") totalPaid  += amt;

                entries.Add(new Dictionary<string, object>
                {
                    { "trans_type",  ttype                          },
                    { "description", row["description"].ToString()  },
                    { "amount",      amt                            },
                    { "acad_year",   row["acadyear"] != DBNull.Value ? row["acadyear"].ToString() : "" },
                    { "semester",    row["semester"] != DBNull.Value ? row["semester"].ToString() : "" },
                    { "trans_date",  row["trans_date"].ToString()   }
                });
            }

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "regno",         regno                    },
                { "total_billed",  totalBilled              },
                { "total_paid",    totalPaid                },
                { "balance",       totalBilled - totalPaid  },
                { "currency",      FinanceEngine.CURRENCY   },
                { "entries",       entries                  }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error fetching residence ledger: " + ex.Message, "SERVER_ERROR");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RESIDENCE REPORT  — staff: per-student allocation + finance summary
    // ═══════════════════════════════════════════════════════════════════

    private void HandleResidenceReport()
    {
        TokenInfo auth = TokenManager.RequireAuth(Request, Response);
        if (auth == null) return;
        if (auth.UserType != "staff") { ApiHelper.Error(Response, "Staff access required.", "FORBIDDEN"); return; }

        string ay     = ApiHelper.RequireParam(Request, Response, "acad_year"); if (ay == null) return;
        int    sem    = ApiHelper.ParamInt(Request, "semester", 1);
        string hallFilter = ApiHelper.Param(Request, "hall_id", "");
        int    page   = Math.Max(1, ApiHelper.ParamInt(Request, "page", 1));
        int    size   = Math.Min(200, Math.Max(1, ApiHelper.ParamInt(Request, "size", 50)));
        int    offset = (page - 1) * size;

        try
        {
            var extraWhere = new System.Text.StringBuilder("");
            var parms = new List<MySqlParameter>
            {
                new MySqlParameter("@ay",  ay),
                new MySqlParameter("@sem", sem)
            };

            if (!string.IsNullOrEmpty(hallFilter))
            {
                extraWhere.Append(" AND ar.hall_id = @hid");
                parms.Add(new MySqlParameter("@hid", Convert.ToInt32(hallFilter)));
            }

            int total = 0;
            try
            {
                total = Convert.ToInt32(ApiHelper.Query(
                    "SELECT COUNT(*) FROM acad_residence ar WHERE ar.acadyear = @ay AND ar.semester = @sem" + extraWhere,
                    parms.ToArray()).Rows[0][0]);
            }
            catch { }

            parms.Add(new MySqlParameter("@lim", size));
            parms.Add(new MySqlParameter("@off", offset));

            DataTable dt = ApiHelper.Query(
                @"SELECT ar.regno,
                         CONCAT(COALESCE(s.firstname,''),' ',COALESCE(s.othername,'')) AS full_name,
                         s.progid, reg.studyyear, reg.residence_status,
                         h.hall_name, ar.room_id,
                         reg.regstatus
                  FROM acad_residence ar
                  JOIN acad_student s ON s.regno = ar.regno
                  JOIN acad_halls h ON h.ID = ar.hall_id
                  LEFT JOIN acad_registration reg ON reg.regno = ar.regno AND reg.acad_year = @ay
                  WHERE ar.acadyear = @ay AND ar.semester = @sem" + extraWhere +
                  " ORDER BY h.hall_name, ar.room_id, s.firstname LIMIT @lim OFFSET @off",
                parms.ToArray()
            );

            ApiHelper.Success(Response, new Dictionary<string, object>
            {
                { "acad_year",   ay                          },
                { "semester",    sem                         },
                { "total",       total                       },
                { "page",        page                        },
                { "pages",       (int)Math.Ceiling(total / (double)size) },
                { "size",        size                        },
                { "allocations", ApiHelper.TableToList(dt)  }
            });
        }
        catch (Exception ex)
        {
            ApiHelper.Error(Response, "Error generating residence report: " + ex.Message, "SERVER_ERROR");
        }
    }
}
