# SignPath preparation

## Eligibility assessment — 10 November 2026

Heimora is a plausible applicant, not an approved project. The [Foundation conditions](https://signpath.org/terms) leave admission to SignPath's discretion and require verifiable project reputation; a newly public repository may need more history.

| Requirement | Evidence or outstanding check |
| --- | --- |
| Public, maintained open-source project | Public repository, GPL-3.0 licence, recent commits and preview releases v0.7.1 and v0.7.2. Confirm ownership of all original code and absence of commercial dual-licensing. |
| Open-source dependencies | Notices are present in vendor/ for PuTTY 0.85, .NET, Npgsql, SQLite and SQLitePCLRaw. Full transitive dependency/licence and bundled-binary provenance review is still required. Windows RDP/DPAPI are operating-system components; confirm acceptance under the System Libraries exception with SignPath. |
| Described, released application | README and release pages describe functionality and preview limitations. The app supports user-selected remote connections; it does not intentionally scan or exploit targets. This is not an exhaustive security audit. |
| Verifiable hosted builds | windows-build.yml builds from checkout on windows-2022 using public NuGet packages and the existing build.ps1. The preparation-branch and main builds passed on 10 November 2026. No local .env or signing token is required. |
| Team accountability and MFA | Roles are proposed in CODE_SIGNING.md. Repository ownership, all write-access accounts' MFA and SignPath MFA must be verified by the owner. |
| Privacy, uninstall and signing policy | CODE_SIGNING.md describes intended data handling. Inno Setup supplies uninstall support; complete installation/update/uninstall validation is outstanding. |
| Foundation acceptance | Not requested or granted. Complete the application at https://signpath.org/apply after reviewing these gaps. |

## Prepared build

The Windows build runs on main pushes, pull requests, version tags and manual dispatch. It publishes an **unsigned workflow artifact**, not a GitHub release. It builds the app, runs the existing self-tests, compiles the installer and creates SHA-256 checksums. Actions are pinned to inspected commit IDs; checkout does not retain credentials. Workflow permissions are read-only and no personal token is used.

The required NuGet runtime/package versions were confirmed present on nuget.org during preparation. GitHub's runner image includes Inno Setup 6. The GUI/RDP/embedded-PuTTY self-tests have passed locally, but may expose runner-specific limitations. Do not skip failed tests merely to get a signing artifact. Test logs are uploaded even on failure. Live remote authentication and PostgreSQL are not covered.

Local actionlint validation and the application/installer version check passed. The owner granted Workflows: Read and write to the local token, resolving the initial push rejection. prepare-signpath was pushed and merged into main at commit 712d22ee822a3d6183cb94b796393786ba380c40. Both hosted runs passed, including self-tests, installer, checksums and artifact uploads:

- Preparation branch: https://github.com/CEWIStudio/Heimora/actions/runs/42816151224
- Main: https://github.com/CEWIStudio/Heimora/actions/runs/42817343922

## Resume the application

Status saved on 10 November 2026: the application form was inspected, but nothing was submitted. No SignPath approval, account configuration or active signing is in place. Existing preview releases remain unsigned. Browser form state was not retained; reopen https://signpath.org/apply.

Still needed from the owner:
- First name, last name and the email address to use for SignPath.
- Whether applying as an individual developer or a company, and company name if applicable.
- Confirmation that all GitHub accounts with write access use MFA; SignPath MFA must also be configured during onboarding.
- Any genuine external users, endorsements or other reputation evidence. Do not invent these.
- Explicit confirmation of the terms and personal-data processing consent before submitting the form. Leave optional marketing consent unchecked unless requested.

Prepared application fields:
- Project name: Heimora.
- Repository/homepage: https://github.com/CEWIStudio/Heimora
- Download page with policy link: https://github.com/CEWIStudio/Heimora#code-signing-policy
- Privacy policy: https://github.com/CEWIStudio/Heimora/blob/main/CODE_SIGNING.md#privacy
- Tagline: An open-source Windows workspace for managing RDP, SSH, web connections and reusable credentials.
- Description: Heimora combines remote connections, folders and reusable credentials in a Windows desktop workspace. It provides a local encrypted vault and optional PostgreSQL metadata storage. The project is GPL-3.0 licensed and currently in preview. Application binaries and installers are built and tested using GitHub-hosted Windows runners.
- Reputation: Newly public repository created on 10 November 2026; two preview releases. At the time checked, installer downloads totalled four (v0.7.1: one; v0.7.2: three), with zero stars and forks. No established community or independent endorsements have been verified. Refresh these figures before submission; they may be internal tests rather than independent users.
- Build system: GitHub Actions.
- Discovery: ChatGPT / Codex, followed by review of official SignPath terms and documentation.

Next step: collect the missing owner details, refresh reputation evidence, complete the form accurately and obtain confirmation before accepting terms/submitting. SignPath can require more public project history. Do not activate signing or claim sponsorship before approval.

The 14-day unsigned artifacts are for inspection only. Do not attach them to a release claiming they are signed. GitHub-hosted standard runners for public repositories are normally free; review GitHub billing/settings before enabling additional services.

## Activation after approval

1. Owner completes the Foundation application and accepts its terms. Do not claim acceptance beforehand. Confirm MFA and team membership.
2. Connect the SignPath GitHub App only to Heimora; configure GitHub.com as the trusted build system. Establish the real organization ID, project slug and manual-approval signing policies with SignPath.
3. Agree on two signing stages: the application payload first, then the Inno installer. Use exact allowlists for Heimora.exe/Heimora.dll and versioned installers, with product-name/version metadata restrictions. Never select all EXE/DLL files in the self-contained runtime or vendor directory.
4. Create a release-only signing workflow using GitHub-hosted runners. Restrict it to reviewed version tags whose commits belong to main; configure protected tags and a GitHub release environment with required manual approval where available. Enforce these restrictions in SignPath too, not solely in editable workflow code.
5. Store the dedicated SignPath submitter credential as SIGNPATH_API_TOKEN in that protected environment. Use actions: read and contents: read for signing. Grant contents: write only to a separate release-publication job if needed. Do not expose signing credentials to pull requests.
6. Follow the [official GitHub integration](https://docs.signpath.io/trusted-build-systems/github): upload the unsigned application payload with upload-artifact v4+, pass that exact artifact ID to signpath/github-action-submit-signing-request, wait for manual approval and download the signed output. Pin the signing action to a reviewed commit before enabling it. Required inputs are api-token, organization-id, project-slug, signing-policy-slug, github-artifact-id and output-artifact-directory.
7. Verify the downloaded application signatures/timestamps and place the signed files in the packaging directory without rebuilding or altering them. Build the installer from that directory. Inno Setup's uninstaller signing must also be integrated and tested with SignPath; agree the supported signing callback/approval sequence before enabling production signing.
8. Upload and submit the installer as a second artifact. Verify its signature, expected publisher, timestamp and packaged application signatures after a test installation. Generate final checksums and publish a new preview release. Do not replace the unsigned v0.7.2 downloads in place.
9. Add the Code signing policy link to every future release/download page and update the policy status only once signing is actually approved and verified.

The signing calls are deliberately not active: no SignPath account identifiers, API credential, approved certificate or tested Inno uninstaller signing integration exists yet. The build and documentation prepare onboarding without claiming an unsigned installer is signed.

## Official references

- https://signpath.org/terms
- https://signpath.org/apply
- https://docs.signpath.io/trusted-build-systems/github
- https://docs.signpath.io/artifact-configuration/examples
- https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md
- https://jrsoftware.org/ishelp/topic_setup_signtool.htm
