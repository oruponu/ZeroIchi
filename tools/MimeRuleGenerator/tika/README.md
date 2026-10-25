# Apache Tika mime type definitions

Imported from Apache Tika (https://github.com/apache/tika).

- Tag: `4.1.0`
- Commit: `c5348700b3f58e5559a50c8e1156d7e2806f42f9`
- Imported: 2026-10-23
- `tika-mimetypes.xml`: `tika-core/src/main/resources/org/apache/tika/mime/tika-mimetypes.xml`
- `LICENSE.txt`, `NOTICE.txt`: the repository root

These files are licensed under the Apache License, Version 2.0. Do not modify them directly; update them from the upstream repository instead.

## Updating

1. Replace the three files with the ones from the new tag, and update the tag, commit and date above.
2. Update the version and commit in `tools/MimeRuleGenerator/Program.cs`.
3. Regenerate the rules from the repository root:

   ```
   dotnet run --project tools/MimeRuleGenerator -- tools/MimeRuleGenerator/tika/tika-mimetypes.xml src/ZeroIchi.Core/Models/FileTypes/TikaMimeRules.g.cs
   ```

4. Update the Apache Tika section of `THIRD-PARTY-NOTICES.md`.
