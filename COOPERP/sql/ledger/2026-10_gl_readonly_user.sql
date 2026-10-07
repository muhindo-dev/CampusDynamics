-- ---------------------------------------------------------------------------
-- General Ledger rebuild: the read-only database account used by every report, check and warning.
-- SELECT only, on the three finance-relevant schemas. Replace <PASSWORD> before running; the real password
-- lives only in web.config (accountsReadOnlyConnectionString). Re-runnable.
-- ---------------------------------------------------------------------------
CREATE USER 'cd_gl_ro'@'localhost' IDENTIFIED BY '<PASSWORD>';
CREATE USER 'cd_gl_ro'@'127.0.0.1' IDENTIFIED BY '<PASSWORD>';
CREATE USER 'cd_gl_ro'@'::1' IDENTIFIED BY '<PASSWORD>';
GRANT SELECT ON campus_dynamics_accounts.* TO 'cd_gl_ro'@'localhost', 'cd_gl_ro'@'127.0.0.1', 'cd_gl_ro'@'::1';
GRANT SELECT ON campus_dynamics.* TO 'cd_gl_ro'@'localhost', 'cd_gl_ro'@'127.0.0.1', 'cd_gl_ro'@'::1';
GRANT SELECT ON campus_dynamics_portal.* TO 'cd_gl_ro'@'localhost', 'cd_gl_ro'@'127.0.0.1', 'cd_gl_ro'@'::1';
FLUSH PRIVILEGES;
-- Undo: DROP USER 'cd_gl_ro'@'localhost'; DROP USER 'cd_gl_ro'@'127.0.0.1'; DROP USER 'cd_gl_ro'@'::1';
