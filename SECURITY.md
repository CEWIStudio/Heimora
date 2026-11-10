# Security

Heimora is a Windows preview. Do not post vulnerabilities, passwords, tokens, database files, audit logs or private host details in public issues.

Use GitHub's **Security → Report a vulnerability** if private vulnerability reporting is enabled on this repository. If that option is unavailable, ask the repository owner for a private reporting channel without disclosing vulnerability details publicly. A private channel has not yet been verified for this preview.

Local records use Windows DPAPI for the current Windows account. This does not protect against malware or an attacker controlling that account. Passwords are temporarily available to the desktop application when connecting. A local audit log is not independent server evidence.

The PostgreSQL integration currently shares connection metadata and credential references. Secrets and audit logs remain local. Central application users, server-enforced permissions and a connection gateway are not implemented.

See [SECURITY-REVIEW.md](docs/SECURITY-REVIEW.md) for the scope and limitations of the local checks. Live RDP/SSH authentication, live PostgreSQL migration, clean-machine installation and an independent penetration test remain outstanding.
