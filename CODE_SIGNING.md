# Code signing policy

## Current status

Heimora is preparing an application to the SignPath Foundation open-source program. Approval has not been granted, and existing releases are unsigned. A checksum verifies file integrity; it is not a digital signature or proof that software is safe.

If accepted, the attribution will be: Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org). The verified publisher will be SignPath Foundation, not CEWIStudio. No sponsorship or approval is claimed by this policy.

## Responsibility

- Committer and reviewer: [CEWIStudio](https://github.com/CEWIStudio).
- Proposed signing approver: CEWIStudio, subject to SignPath onboarding and verification.
- Contributors' changes require maintainer review before release. Every signing request requires explicit manual approval. All contributors with repository write access and all SignPath team members must enable multi-factor authentication before signing is activated; this has not yet been verified.

## What may be signed

Only Heimora's own Windows application files (`Heimora.exe` and `Heimora.dll`) and its `Heimora-Setup-<version>-win-x64.exe` installer, built from the public [CEWIStudio/Heimora repository](https://github.com/CEWIStudio/Heimora), may be submitted under this policy. Product metadata must identify Heimora and the release version.

Builds must run on GitHub-hosted Windows runners using the reviewed workflow at the release commit. The commit must be on `main`, and its release tag must match the application and installer version. Approvers inspect the commit, successful build/self-test results, dependency changes and signing scope. Pull-request builds are unsigned inspection artifacts and must never receive signing credentials or production signing approval.

Bundled third-party files, including PuTTY, the .NET runtime, Npgsql and SQLite components, retain their upstream identity and licences. They must not be re-signed as Heimora's own code. Changes to dependencies and their provenance require review.

The application is signed before packaging. The completed installer is signed separately, including its uninstaller through Inno Setup's supported signing integration. Valid Authenticode signatures, expected signer and timestamps must be verified before release. Checksums are generated only after signing. Signed downloads must never be silently replaced under an existing release version.

## Privacy

Heimora has no intentional background telemetry or automatic crash reporting. Connections to RDP, SSH and PostgreSQL targets, browser links and issue reporting are initiated by the user or operator. Issue reporting opens a GitHub form for the user to review; vaults, passwords and audit logs must not be included. Browsers, remote services and third-party components have their own privacy behaviour; review the destination service before connecting. GitHub's [privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) applies when using GitHub links. Local data and security limitations are described in [SECURITY.md](SECURITY.md).

Never submit private vaults, tokens, credentials or user data for signing. Signing access is restricted to approved maintainers and the release workflow. A compromised signing identity or suspicious release must halt signing until investigated; contact SignPath for revocation where necessary.

Setup steps, eligibility evidence and outstanding checks are tracked in [SignPath preparation](docs/SIGNPATH.md).
