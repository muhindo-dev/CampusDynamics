<%@ Page Language="C#" MasterPageFile="~/COOPERP/NewScreens/SidebarMaster.master" AutoEventWireup="true"
    CodeFile="TicketsController.aspx.cs" Inherits="NewScreens_TicketsController"
    Title="Support Tickets | Campus Dynamics" %>

<asp:Content ID="head" ContentPlaceHolderID="HeadContent" runat="server">
<style>
/* ── TicketsController – compact admin UI ──────────────────────────────────── */
*,*::before,*::after{box-sizing:border-box;}

/* Top toolbar */
.tc-toolbar{display:flex;align-items:center;gap:8px;padding:8px 14px;background:#fff;
    border-bottom:1px solid #e0e5ed;flex-wrap:nowrap;}
.tc-toolbar__title{font-size:14px;font-weight:700;color:#05275C;white-space:nowrap;
    display:flex;align-items:center;gap:7px;flex-shrink:0;}
.tc-toolbar__title svg{width:16px;height:16px;color:#174DA4;}
.tc-search-input{padding:6px 9px;border:1px solid #cdd3de;font-size:12px;
    font-family:inherit;outline:none;width:220px;}
.tc-search-input:focus{border-color:#174DA4;}
.tc-cat-select{padding:6px 8px;border:1px solid #cdd3de;font-size:12px;
    font-family:inherit;background:#fff;outline:none;}
.tc-tb-btn{padding:6px 12px;font-size:12px;font-weight:600;border:none;cursor:pointer;
    white-space:nowrap;transition:background .12s;}
.tc-tb-btn--primary{background:#05275C;color:#fff;}
.tc-tb-btn--primary:hover{background:#041d45;}
.tc-tb-btn--ghost{background:#fff;color:#555;border:1px solid #cdd3de;}
.tc-tb-btn--ghost:hover{background:#f5f7fa;}
.tc-tb-spacer{flex:1;}

/* Stats toggle button */
.tc-stats-toggle{display:inline-flex;align-items:center;gap:3px;padding:3px 8px;
    font-size:10px;font-weight:600;color:#888;border:1px solid #e0e5ed;background:#fff;
    cursor:pointer;white-space:nowrap;transition:all .12s;flex-shrink:0;}
.tc-stats-toggle:hover{border-color:#174DA4;color:#174DA4;}
.tc-stats-toggle svg{width:10px;height:10px;transition:transform .2s;}
.tc-stats-wrap.is-collapsed .tc-stats{display:none;}
.tc-stats-wrap.is-collapsed .tc-stats-toggle svg{transform:rotate(180deg);}

/* Stats + filter strip (combined) */
.tc-stats{display:flex;background:#fff;border-bottom:1px solid #e0e5ed;overflow-x:auto;
    -webkit-overflow-scrolling:touch;flex-shrink:0;}
.tc-stat{flex:1;min-width:66px;display:flex;flex-direction:column;align-items:center;
    justify-content:center;padding:4px 6px;cursor:pointer;border-bottom:2px solid transparent;
    transition:background .1s,border-color .15s;border-right:1px solid #f5f5f5;white-space:nowrap;}
.tc-stat:last-child{border-right:none;}
.tc-stat:hover{background:#f5f7fa;}
.tc-stat.is-active{background:#eff3fc;border-bottom-color:#05275C;}
.tc-stat__num{font-size:15px;font-weight:800;line-height:1;color:#05275C;}
.tc-stat__lbl{font-size:8px;color:#bbb;margin-top:2px;text-transform:uppercase;
    letter-spacing:.2px;font-weight:600;}
.tc-stat--open .tc-stat__num     {color:#174DA4;}
.tc-stat--prog .tc-stat__num     {color:#d97706;}
.tc-stat--wait .tc-stat__num     {color:#7c3aed;}
.tc-stat--resolved .tc-stat__num {color:#16a34a;}
.tc-stat--closed .tc-stat__num   {color:#6b7280;}
.tc-stat--urgent .tc-stat__num   {color:#dc3545;}

/* Two-pane layout */
.tc-layout{display:flex;overflow:hidden;}
.tc-list-pane{width:330px;flex-shrink:0;border-right:1px solid #e0e5ed;
    display:flex;flex-direction:column;background:#fff;}
.tc-list-scroll{flex:1;overflow-y:auto;}
.tc-detail-pane{flex:1;display:flex;flex-direction:column;background:#f5f7fa;min-width:0;}

/* List rows */
.tc-row{padding:10px 13px;border-bottom:1px solid #f2f2f2;cursor:pointer;
    transition:background .1s;border-left:3px solid transparent;}
.tc-row:hover{background:#f8f9fc;}
.tc-row.is-selected{background:#eff3fc;border-left-color:#05275C;}
.tc-row.is-unread .tc-row__subject{font-weight:700;}
.tc-row__top{display:flex;align-items:center;justify-content:space-between;gap:6px;margin-bottom:3px;}
.tc-row__ref{font-size:9px;font-weight:700;color:#bbb;font-family:Consolas,monospace;letter-spacing:.3px;}
.tc-badge{display:inline-block;padding:2px 6px;font-size:9px;font-weight:700;
    letter-spacing:.3px;text-transform:uppercase;}
.tc-row__subject{font-size:12px;font-weight:500;color:#1a1a2e;overflow:hidden;
    text-overflow:ellipsis;white-space:nowrap;margin-bottom:3px;}
.tc-row__meta{font-size:10px;color:#aaa;display:flex;align-items:center;gap:5px;flex-wrap:nowrap;overflow:hidden;}
.tc-row__dot{width:6px;height:6px;border-radius:50%;flex-shrink:0;}
.tc-row__name{overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1;min-width:0;}
.tc-row__urg{font-size:9px;font-weight:700;color:#dc3545;background:rgba(220,53,69,.1);
    padding:1px 4px;flex-shrink:0;text-transform:uppercase;}
.tc-row__ago{font-size:9px;color:#ccc;flex-shrink:0;white-space:nowrap;}

/* List footer */
.tc-list-footer{padding:7px 12px;border-top:1px solid #f0f0f0;background:#fafafa;
    display:flex;align-items:center;justify-content:space-between;flex-shrink:0;}
.tc-page-info{font-size:10px;color:#aaa;}
.tc-pager{display:flex;gap:3px;}
.tc-pager a{padding:3px 8px;border:1px solid #e0e5ed;color:#555;text-decoration:none;
    font-size:10px;font-weight:600;transition:background .1s;}
.tc-pager a:hover{background:#f0f4f8;}
.tc-pager a.is-active{background:#05275C;color:#fff;border-color:#05275C;}

/* Empty */
.tc-empty{padding:36px 20px;text-align:center;color:#bbb;font-size:12px;}
.tc-empty svg{width:32px;height:32px;color:#ddd;display:block;margin:0 auto 10px;}

/* Detail placeholder */
.tc-detail-placeholder{display:flex;flex-direction:column;align-items:center;
    justify-content:center;height:100%;color:#ccc;font-size:12px;gap:10px;}
.tc-detail-placeholder svg{width:44px;height:44px;color:#e0e0e0;}

/* Detail pane panels */
.tc-dh{background:#fff;border-bottom:1px solid #e0e5ed;padding:12px 16px;flex-shrink:0;}
.tc-dh__top{display:flex;align-items:flex-start;justify-content:space-between;gap:10px;}
.tc-dh__ref{font-size:9px;font-weight:700;color:#bbb;font-family:Consolas,monospace;letter-spacing:.4px;}
.tc-dh__subject{font-size:14px;font-weight:700;color:#05275C;margin:4px 0 6px;line-height:1.3;}
.tc-dh__badges{display:flex;flex-wrap:wrap;gap:4px;}
.tc-dh__meta{font-size:10px;color:#999;display:flex;gap:12px;flex-wrap:wrap;
    margin-top:8px;padding-top:8px;border-top:1px solid #f5f5f5;}
.tc-dh__meta strong{color:#555;}

/* Controls bar */
.tc-controls{background:#fafafa;border-bottom:1px solid #e0e5ed;padding:7px 14px;
    display:flex;align-items:center;gap:8px;flex-wrap:wrap;flex-shrink:0;}
.tc-ctrl-lbl{font-size:10px;font-weight:700;color:#888;text-transform:uppercase;
    letter-spacing:.2px;white-space:nowrap;}
.tc-ctrl-select{padding:4px 7px;border:1px solid #cdd3de;font-size:11px;
    font-family:inherit;background:#fff;outline:none;cursor:pointer;}
.tc-ctrl-select:focus{border-color:#174DA4;}
.tc-ctrl-sep{width:1px;height:20px;background:#e0e5ed;flex-shrink:0;}
.tc-ctrl-input{padding:4px 7px;border:1px solid #cdd3de;font-size:11px;
    font-family:inherit;outline:none;width:130px;}
.tc-ctrl-input:focus{border-color:#174DA4;}
.tc-ctrl-btn{padding:4px 10px;font-size:11px;font-weight:600;border:none;cursor:pointer;
    background:#05275C;color:#fff;transition:background .12s;white-space:nowrap;}
.tc-ctrl-btn:hover{background:#041d45;}
.tc-ctrl-btn--danger{background:#dc3545;}
.tc-ctrl-btn--danger:hover{background:#b02a37;}
.tc-ctrl-btn--green{background:#16a34a;}
.tc-ctrl-btn--green:hover{background:#138d40;}
.tc-action-msg{font-size:10px;font-weight:600;padding:3px 8px;border-radius:2px;
    opacity:0;transition:opacity .2s;}
.tc-action-msg.is-saving{opacity:1;color:#888;}
.tc-action-msg.is-ok{opacity:1;color:#16a34a;}
.tc-action-msg.is-err{opacity:1;color:#dc3545;}

/* Thread */
.tc-thread{flex:1;overflow-y:auto;padding:14px 16px;display:flex;
    flex-direction:column;gap:7px;}
.tc-msg{display:flex;flex-direction:column;max-width:82%;}
.tc-msg--sub   {align-self:flex-end;align-items:flex-end;}
.tc-msg--admin {align-self:flex-start;align-items:flex-start;}
.tc-bubble{padding:9px 13px;font-size:12px;line-height:1.6;white-space:pre-wrap;word-break:break-word;}
.tc-msg--sub   .tc-bubble{background:#05275C;color:#fff;border-radius:14px 14px 4px 14px;}
.tc-msg--admin .tc-bubble{background:#fff;color:#1a1a2e;border:1px solid #e0e5ed;border-radius:14px 14px 14px 4px;}
.tc-msg--internal .tc-bubble{background:#fef9c3;border:1px dashed #d97706;border-radius:6px;color:#92400e;}
.tc-msg__meta{font-size:10px;color:#aaa;margin-top:2px;}
.tc-msg--sub .tc-msg__meta{text-align:right;}
.tc-event{align-self:center;background:#f0f0f0;border:1px solid #e8e8e8;padding:3px 12px;
    font-size:10px;color:#888;border-radius:20px;margin:2px 0;}
.tc-att-link{display:inline-flex;align-items:center;gap:4px;font-size:11px;
    padding:3px 7px;border-radius:3px;text-decoration:none;margin-top:5px;}
.tc-msg--sub   .tc-att-link{background:rgba(255,255,255,.18);color:rgba(255,255,255,.9);}
.tc-msg--admin .tc-att-link{background:#f5f7fa;color:#174DA4;border:1px solid #e0e5ed;}
.tc-att-img{max-width:160px;max-height:120px;display:block;margin-top:5px;
    cursor:pointer;object-fit:cover;border-radius:3px;}

/* Thread loading spinner */
.tc-thread-loading{display:flex;align-items:center;justify-content:center;
    height:60px;gap:8px;color:#aaa;font-size:12px;}
.tc-spinner{width:16px;height:16px;border:2px solid #e0e5ed;border-top-color:#174DA4;
    border-radius:50%;animation:tc-spin .7s linear infinite;}
@keyframes tc-spin{to{transform:rotate(360deg);}}

/* Reply area */
.tc-reply{background:#fff;border-top:1px solid #e0e5ed;padding:10px 14px;flex-shrink:0;}
.tc-reply-ta{width:100%;padding:7px 9px;border:1px solid #cdd3de;font-size:12px;
    font-family:inherit;resize:vertical;min-height:68px;max-height:160px;
    box-sizing:border-box;outline:none;line-height:1.5;}
.tc-reply-ta:focus{border-color:#174DA4;}
.tc-reply-row{display:flex;align-items:center;gap:8px;margin-top:7px;flex-wrap:wrap;}
.tc-internal-chk{display:flex;align-items:center;gap:4px;font-size:11px;color:#888;cursor:pointer;}
.tc-internal-chk input{cursor:pointer;}
.tc-attach-label{display:inline-flex;align-items:center;gap:4px;padding:5px 9px;
    font-size:11px;font-weight:500;color:#555;border:1px dashed #cdd3de;
    background:#f9fafb;cursor:pointer;transition:all .12s;}
.tc-attach-label:hover{border-color:#174DA4;color:#174DA4;}
.tc-attach-label svg{width:12px;height:12px;}
.tc-attach-label input[type=file]{display:none;}
.tc-reply-send{padding:6px 16px;background:#05275C;color:#fff;border:none;font-size:12px;
    font-weight:600;cursor:pointer;margin-left:auto;transition:background .12s;
    display:inline-flex;align-items:center;gap:5px;}
.tc-reply-send:hover{background:#041d45;}
.tc-reply-send:disabled{background:#a0afc0;cursor:not-allowed;}
.tc-file-list{display:flex;flex-direction:column;gap:3px;margin-top:5px;}
.tc-file-item{display:flex;align-items:center;gap:5px;font-size:10px;padding:3px 7px;
    background:#f5f7fa;border:1px solid #e0e5ed;}
.tc-file-item__name{flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;color:#555;}
.tc-file-item__del{background:none;border:none;color:#dc3545;cursor:pointer;
    font-size:13px;line-height:1;padding:0 2px;margin-left:auto;}

/* Lightbox */
.tc-lightbox{display:none;position:fixed;inset:0;background:rgba(0,0,0,.86);
    z-index:9999;align-items:center;justify-content:center;}
.tc-lightbox.is-open{display:flex;}
.tc-lightbox img{max-width:90vw;max-height:90vh;object-fit:contain;}
.tc-lightbox__close{position:absolute;top:14px;right:18px;font-size:26px;
    color:#fff;cursor:pointer;background:none;border:none;line-height:1;}

@media(max-width:900px){
    .tc-list-pane{width:280px;}
}
@media(max-width:680px){
    .tc-layout{flex-direction:column;}
    .tc-list-pane{width:100%;height:240px;flex-shrink:0;}
}
</style>
</asp:Content>

<asp:Content ID="main" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

<div class="tc-wrap">

    <!-- ── Toolbar ───────────────────────────────────────────────────────────── -->
    <div class="tc-toolbar">
        <span class="tc-toolbar__title">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
            </svg>
            Support Tickets
        </span>
        <asp:TextBox ID="txtSearch" runat="server" CssClass="tc-search-input"
            placeholder="Search name, reg no, subject…" MaxLength="120" />
        <asp:DropDownList ID="ddlIssue" runat="server" CssClass="tc-cat-select">
            <asp:ListItem Value="ALL">All Categories</asp:ListItem>
            <asp:ListItem Value="Academic">Academic</asp:ListItem>
            <asp:ListItem Value="Financial">Financial</asp:ListItem>
            <asp:ListItem Value="Registration">Registration</asp:ListItem>
            <asp:ListItem Value="Other">Other</asp:ListItem>
        </asp:DropDownList>
        <asp:Button ID="btnSearch" runat="server" Text="Filter" CssClass="tc-tb-btn tc-tb-btn--primary"
            OnClick="btnSearch_Click" />
        <asp:Button ID="btnReset"  runat="server" Text="Reset"  CssClass="tc-tb-btn tc-tb-btn--ghost"
            OnClick="btnReset_Click" />
        <button type="button" class="tc-stats-toggle" id="tcStatsToggle" onclick="toggleStats()" title="Show/hide stats">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <polyline points="18 15 12 9 6 15"/>
            </svg>
            Stats
        </button>
    </div>

    <!-- ── Stats + filter strip ──────────────────────────────────────────────── -->
    <div class="tc-stats-wrap" id="tcStatsWrap">
    <div class="tc-stats">
        <div class="tc-stat<%= _statusFilter=="ALL"?" is-active":"" %>"        onclick="applyFilter('ALL')">
            <div class="tc-stat__num"><span id="stat_total"><asp:Literal ID="litStatTotal"    runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">All</div>
        </div>
        <div class="tc-stat tc-stat--open<%= _statusFilter=="OPEN"?" is-active":"" %>"  onclick="applyFilter('OPEN')">
            <div class="tc-stat__num"><span id="stat_open"><asp:Literal ID="litStatOpen"      runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">Open</div>
        </div>
        <div class="tc-stat tc-stat--prog<%= _statusFilter=="IN_PROGRESS"?" is-active":"" %>" onclick="applyFilter('IN_PROGRESS')">
            <div class="tc-stat__num"><span id="stat_prog"><asp:Literal ID="litStatProg"      runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">In Progress</div>
        </div>
        <div class="tc-stat tc-stat--wait<%= _statusFilter=="AWAITING_REPLY"?" is-active":"" %>" onclick="applyFilter('AWAITING_REPLY')">
            <div class="tc-stat__num"><span id="stat_wait"><asp:Literal ID="litStatWait"      runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">Awaiting</div>
        </div>
        <div class="tc-stat tc-stat--resolved<%= _statusFilter=="RESOLVED"?" is-active":"" %>" onclick="applyFilter('RESOLVED')">
            <div class="tc-stat__num"><span id="stat_resolved"><asp:Literal ID="litStatResolved" runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">Resolved</div>
        </div>
        <div class="tc-stat tc-stat--closed<%= _statusFilter=="CLOSED"?" is-active":"" %>"  onclick="applyFilter('CLOSED')">
            <div class="tc-stat__num"><span id="stat_closed"><asp:Literal ID="litStatClosed"  runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">Closed</div>
        </div>
        <div class="tc-stat tc-stat--urgent" onclick="applyFilter('OPEN')">
            <div class="tc-stat__num"><span id="stat_urgent"><asp:Literal ID="litStatUrgent"  runat="server">0</asp:Literal></span></div>
            <div class="tc-stat__lbl">⚡ Urgent</div>
        </div>
    </div>
    </div>

    <!-- ── Two-pane layout ───────────────────────────────────────────────────── -->
    <div class="tc-layout" id="tcLayout">

        <!-- Left: ticket list -->
        <div class="tc-list-pane">
            <div class="tc-list-scroll">
                <asp:Literal ID="litTicketList" runat="server" />
            </div>
            <div class="tc-list-footer">
                <span class="tc-page-info"><asp:Literal ID="litPagingInfo" runat="server" /></span>
                <div class="tc-pager"><asp:Literal ID="litPager" runat="server" /></div>
            </div>
        </div>

        <!-- Right: detail (AJAX-driven) -->
        <div class="tc-detail-pane">

            <!-- Placeholder -->
            <div class="tc-detail-placeholder" id="tcPlaceholder">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2">
                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
                </svg>
                Select a ticket to view
            </div>

            <!-- Detail shell (shown after AJAX load) -->
            <div id="tcDetail" style="display:none;flex-direction:column;height:100%;overflow:hidden;">

                <!-- Header -->
                <div class="tc-dh">
                    <div class="tc-dh__top">
                        <div style="min-width:0;flex:1">
                            <div class="tc-dh__ref" id="tcDRef"></div>
                            <div class="tc-dh__subject" id="tcDSubject"></div>
                            <div class="tc-dh__badges" id="tcDBadges"></div>
                        </div>
                    </div>
                    <div class="tc-dh__meta">
                        <span>By: <strong id="tcDSubmitter"></strong></span>
                        <span>Opened: <span id="tcDCreated"></span></span>
                        <span>Updated: <span id="tcDUpdated"></span></span>
                        <span id="tcDAssignedWrap" style="display:none">Handler: <strong id="tcDAssigned"></strong></span>
                    </div>
                </div>

                <!-- Controls bar -->
                <div class="tc-controls">
                    <span class="tc-ctrl-lbl">Status</span>
                    <select id="tcDdlStatus" class="tc-ctrl-select" onchange="ajaxAction('set_status',this.value)">
                        <option value="OPEN">Open</option>
                        <option value="IN_PROGRESS">In Progress</option>
                        <option value="AWAITING_REPLY">Awaiting Reply</option>
                        <option value="RESOLVED">Resolved</option>
                        <option value="CLOSED">Closed</option>
                    </select>
                    <div class="tc-ctrl-sep"></div>
                    <span class="tc-ctrl-lbl">Priority</span>
                    <select id="tcDdlPriority" class="tc-ctrl-select" onchange="ajaxAction('set_priority',this.value)">
                        <option value="LOW">Low</option>
                        <option value="NORMAL">Normal</option>
                        <option value="HIGH">High</option>
                        <option value="URGENT">Urgent</option>
                    </select>
                    <div class="tc-ctrl-sep"></div>
                    <span class="tc-ctrl-lbl">Assign</span>
                    <input type="text" id="tcTxtAssign" class="tc-ctrl-input" placeholder="Name or username" />
                    <button type="button" class="tc-ctrl-btn" onclick="ajaxAction('assign',document.getElementById('tcTxtAssign').value)">Assign</button>
                    <div class="tc-ctrl-sep"></div>
                    <button type="button" class="tc-ctrl-btn tc-ctrl-btn--green"
                            onclick="ajaxAction('set_status','RESOLVED')">&#10003; Resolve</button>
                    <button type="button" class="tc-ctrl-btn tc-ctrl-btn--danger"
                            onclick="ajaxAction('set_status','CLOSED')">&#10005; Close</button>
                    <% if (DcAccess.CanAny(DcAccess.Report, DcAccess.Manage, DcAccess.ManageAll, DcAccess.ManageExam)) { %>
                    <div class="tc-ctrl-sep"></div>
                    <a id="tcDcLink" class="tc-ctrl-btn" href="DisciplinaryRecords.aspx?new=1" target="_blank" rel="noopener" title="Report this complaint as a disciplinary case">Open disciplinary case</a>
                    <% } %>
                    <span id="tcActionMsg" class="tc-action-msg"></span>
                </div>

                <!-- Thread -->
                <div class="tc-thread" id="tcThread"></div>

                <!-- Reply area -->
                <div class="tc-reply">
                    <textarea id="tcReplyTa" class="tc-reply-ta" rows="3"
                        placeholder="Type your reply to the student…"
                        onkeydown="if(event.ctrlKey&&event.key==='Enter')ajaxReply()"></textarea>
                    <div class="tc-reply-row">
                        <label class="tc-internal-chk">
                            <input type="checkbox" id="tcChkInternal" />
                            Internal note
                        </label>
                        <label class="tc-attach-label">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>
                            </svg>
                            Attach
                            <input type="file" id="tcFileInput" multiple
                                accept=".jpg,.jpeg,.png,.gif,.pdf,.doc,.docx,.txt"
                                onchange="handleAdminFiles(this.files)" />
                        </label>
                        <span id="tcAttCount" style="font-size:10px;color:#888;display:none"></span>
                        <button type="button" id="tcSendBtn" class="tc-reply-send" onclick="ajaxReply()">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13">
                                <line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/>
                            </svg>
                            Send
                        </button>
                    </div>
                    <div class="tc-file-list" id="tcAdminFileList"></div>
                </div>

            </div>
        </div>
    </div>
</div>

<!-- Lightbox -->
<div class="tc-lightbox" id="tcLightbox" onclick="closeLightbox()">
    <button type="button" class="tc-lightbox__close" onclick="closeLightbox()">&times;</button>
    <img id="tcLightboxImg" src="" alt="" />
</div>

<script>
(function () {
'use strict';

// ── Layout height ────────────────────────────────────────────────────────────
function adjustLayout() {
    var layout = document.getElementById('tcLayout');
    if (!layout) return;
    var top = layout.getBoundingClientRect().top + window.pageYOffset;
    layout.style.height = (window.innerHeight - top - 1) + 'px';
}
window.addEventListener('load', adjustLayout);
window.addEventListener('resize', adjustLayout);

// ── Stats toggle ─────────────────────────────────────────────────────────────
window.toggleStats = function() {
    var wrap = document.getElementById('tcStatsWrap');
    if (!wrap) return;
    wrap.classList.toggle('is-collapsed');
    // Re-measure layout after animation
    setTimeout(adjustLayout, 50);
    try { localStorage.setItem('tc_stats_hidden', wrap.classList.contains('is-collapsed') ? '1' : '0'); } catch(e) {}
};
// Restore collapsed state
(function() {
    try {
        if (localStorage.getItem('tc_stats_hidden') === '1') {
            var wrap = document.getElementById('tcStatsWrap');
            if (wrap) { wrap.classList.add('is-collapsed'); setTimeout(adjustLayout, 0); }
        }
    } catch(e) {}
}());

// ── State ────────────────────────────────────────────────────────────────────
var tcId   = 0;
var tcFiles = [];
var TC_URL  = 'TicketAction.ashx';

// ── Filter navigation ────────────────────────────────────────────────────────
window.applyFilter = function(s) {
    var qs = '?sf=' + encodeURIComponent(s) +
             '&if=' + encodeURIComponent(valOf('<%= ddlIssue.ClientID %>')) +
             '&q='  + encodeURIComponent(valOf('<%= txtSearch.ClientID %>'));
    window.location.href = qs;
};

// ── Ticket selection (AJAX) ──────────────────────────────────────────────────
window.selectTicket = function(id) {
    if (tcId === id) return;
    tcId = id;

    document.querySelectorAll('.tc-row').forEach(function(r) { r.classList.remove('is-selected'); });
    var row = document.getElementById('row_' + id);
    if (row) row.classList.add('is-selected');

    showDetailLoading();

    tcFetch(TC_URL + '?action=detail&id=' + id, null)
        .then(function(d) {
            if (d.ok) renderDetail(d);
            else      showDetailError(d.msg);
        })
        .catch(function(err) { showDetailError(String(err.message || err)); });
};

function showDetailLoading() {
    document.getElementById('tcPlaceholder').style.display = 'none';
    var det = document.getElementById('tcDetail');
    det.style.display = 'flex';
    document.getElementById('tcThread').innerHTML =
        '<div class="tc-thread-loading"><div class="tc-spinner"></div> Loading…</div>';
}

function showDetailError(msg) {
    document.getElementById('tcThread').innerHTML =
        '<div style="padding:20px;color:#dc3545;font-size:12px;">' + escHtml(msg) + '</div>';
}

function renderDetail(d) {
    setText('tcDRef',       d.ref);
    setText('tcDSubject',   d.subject);
    setHtml('tcDBadges',    d.badges);
    setText('tcDSubmitter', d.submitter);
    setText('tcDCreated',   d.created);
    setText('tcDUpdated',   d.updated);

    var aw = document.getElementById('tcDAssignedWrap');
    if (d.assigned) {
        setText('tcDAssigned', d.assigned);
        aw.style.display = '';
    } else {
        aw.style.display = 'none';
    }

    var dcl = document.getElementById('tcDcLink');
    if (dcl) dcl.href = 'DisciplinaryRecords.aspx?new=1&ticket=' + encodeURIComponent(tcId || '');
    setVal('tcDdlStatus',   d.status);
    setVal('tcDdlPriority', d.priority);
    setVal('tcTxtAssign',   d.assigned);

    var thread = document.getElementById('tcThread');
    thread.innerHTML = d.thread;
    thread.scrollTop = thread.scrollHeight;

    clearActionMsg();
    document.getElementById('tcReplyTa').value = '';
    tcFiles = [];
    renderFileList();
}

// ── Core fetch wrapper — surfaces real HTTP errors instead of swallowing them ──
function tcFetch(url, postBody) {
    var opts = postBody
        ? { method: 'POST', body: postBody }
        : { method: 'GET' };

    // For plain key=value strings set Content-Type; FormData sets it automatically
    if (typeof postBody === 'string') {
        opts.headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
    }

    return fetch(url, opts).then(function(r) {
        if (!r.ok) {
            return r.text().then(function(body) {
                // Show first 300 chars of the server's error page for diagnosis
                throw new Error('HTTP ' + r.status + ' — ' + body.replace(/<[^>]+>/g,'').trim().substring(0, 300));
            });
        }
        return r.json().catch(function() {
            throw new Error('Invalid JSON response from ' + url);
        });
    });
}

// ── AJAX actions (status, priority, assign) ──────────────────────────────────
window.ajaxAction = function(action, value) {
    if (!tcId) return;
    if (value === undefined || value === null) value = '';
    value = String(value).trim();

    setActionMsg('saving');

    var body = 'action=' + encodeURIComponent(action) +
               '&id='    + tcId +
               '&value=' + encodeURIComponent(value);

    tcFetch(TC_URL, body)
        .then(function(d) {
            if (d.ok) {
                setActionMsg('ok');
                if (d.stats) updateStats(d.stats);
                if (action === 'set_status') reloadDetailHeader();
            } else {
                setActionMsg('err', d.msg || 'Error');
            }
        })
        .catch(function(err) { setActionMsg('err', String(err.message || err)); });
};

function reloadDetailHeader() {
    if (!tcId) return;
    tcFetch(TC_URL + '?action=detail&id=' + tcId, null)
        .then(function(d) {
            if (d.ok) {
                setHtml('tcDBadges', d.badges);
                setVal('tcDdlStatus', d.status);
            }
        })
        .catch(function() {});
}

// ── AJAX reply ────────────────────────────────────────────────────────────────
window.ajaxReply = function() {
    if (!tcId) return;
    var ta   = document.getElementById('tcReplyTa');
    var text = ta ? ta.value.trim() : '';
    if (!text) { alert('Please type a reply first.'); return; }

    var isInternal = document.getElementById('tcChkInternal').checked ? '1' : '0';
    var btn        = document.getElementById('tcSendBtn');
    var sendIcon   = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13">' +
                     '<line x1="22" y1="2" x2="11" y2="13"/><polygon points="22 2 15 22 11 13 2 9 22 2"/></svg> Send';

    btn.disabled = true;
    btn.textContent = 'Sending…';

    var fd = new FormData();
    fd.append('action',   'reply');
    fd.append('id',       tcId);
    fd.append('text',     text);
    fd.append('internal', isInternal);
    tcFiles.forEach(function(f) { fd.append('files[]', f); });

    tcFetch(TC_URL, fd)
        .then(function(d) {
            if (d.ok) {
                ta.value = '';
                document.getElementById('tcChkInternal').checked = false;
                tcFiles = [];
                renderFileList();
                var thread = document.getElementById('tcThread');
                thread.innerHTML = d.thread;
                thread.scrollTop = thread.scrollHeight;
                if (d.stats) updateStats(d.stats);
            } else {
                alert(d.msg || 'Error sending reply');
            }
            btn.disabled = false; btn.innerHTML = sendIcon;
        })
        .catch(function(err) {
            alert(String(err.message || err));
            btn.disabled = false; btn.innerHTML = sendIcon;
        });
};

// ── File handling ─────────────────────────────────────────────────────────────
window.handleAdminFiles = function(files) {
    for (var i = 0; i < files.length; i++) {
        if (tcFiles.length >= 3) break;
        var f = files[i];
        if (f.size > 5 * 1024 * 1024) continue;
        var ext = f.name.substring(f.name.lastIndexOf('.')).toLowerCase();
        if (['.jpg','.jpeg','.png','.gif','.pdf','.doc','.docx','.txt'].indexOf(ext) === -1) continue;
        tcFiles.push(f);
    }
    renderFileList();
    // Reset so same file can be picked again
    document.getElementById('tcFileInput').value = '';
};

window.removeAdminFile = function(i) { tcFiles.splice(i, 1); renderFileList(); };

function renderFileList() {
    var list  = document.getElementById('tcAdminFileList');
    var count = document.getElementById('tcAttCount');
    list.innerHTML = '';
    tcFiles.forEach(function(f, i) {
        var item = document.createElement('div');
        item.className = 'tc-file-item';
        item.innerHTML = '<span class="tc-file-item__name">' + escHtml(f.name) + '</span>' +
            '<button type="button" class="tc-file-item__del" onclick="removeAdminFile(' + i + ')">&times;</button>';
        list.appendChild(item);
    });
    if (count) {
        if (tcFiles.length > 0) {
            count.textContent = tcFiles.length + ' file' + (tcFiles.length > 1 ? 's' : '') + ' attached';
            count.style.display = '';
        } else {
            count.style.display = 'none';
        }
    }
}

// ── Stats update ──────────────────────────────────────────────────────────────
function updateStats(s) {
    var keys = { total:'stat_total', open:'stat_open', prog:'stat_prog',
                 wait:'stat_wait', resolved:'stat_resolved', closed:'stat_closed', urgent:'stat_urgent' };
    for (var k in keys) {
        var el = document.getElementById(keys[k]);
        if (el && s[k] !== undefined) el.textContent = s[k];
    }
}

// ── Action message ────────────────────────────────────────────────────────────
var _msgTimer = null;
function setActionMsg(state, text) {
    var el = document.getElementById('tcActionMsg');
    if (!el) return;
    clearTimeout(_msgTimer);
    if (state === 'saving') {
        el.textContent = '⟳ Saving…';
        el.className   = 'tc-action-msg is-saving';
    } else if (state === 'ok') {
        el.textContent = '✓ Saved';
        el.className   = 'tc-action-msg is-ok';
        _msgTimer = setTimeout(function() { el.textContent=''; el.className='tc-action-msg'; }, 2200);
    } else {
        el.textContent = '✕ ' + (text || 'Error');
        el.className   = 'tc-action-msg is-err';
    }
}
function clearActionMsg() {
    var el = document.getElementById('tcActionMsg');
    if (el) { el.textContent = ''; el.className = 'tc-action-msg'; }
}

// ── Lightbox ──────────────────────────────────────────────────────────────────
window.openLightbox  = function(src) {
    document.getElementById('tcLightboxImg').src = src;
    document.getElementById('tcLightbox').classList.add('is-open');
};
window.closeLightbox = function() {
    document.getElementById('tcLightbox').classList.remove('is-open');
};
document.addEventListener('keydown', function(e) { if (e.key === 'Escape') closeLightbox(); });

// ── Utilities ─────────────────────────────────────────────────────────────────
function setText(id, val) { var e = document.getElementById(id); if (e) e.textContent = val || ''; }
function setHtml(id, val) { var e = document.getElementById(id); if (e) e.innerHTML  = val || ''; }
function setVal(id, val)  { var e = document.getElementById(id); if (e) e.value      = val || ''; }
function valOf(id)        { var e = document.getElementById(id); return e ? e.value : ''; }
function escHtml(s) {
    return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

}());
</script>
</asp:Content>
