# T-004 — `System.Drawing.Common` is Windows-only in the ModKit

- **Priority**: 🟠 Medium
- **Type**: Portability

## Context

`System.Drawing.Common 8.0.0` is referenced by **OpenTPW.ModKit**. Since .NET 7 this
package **throws `PlatformNotSupportedException` off Windows** (official Microsoft
decision). The ModKit therefore will not run on Linux.

## Proposed fix

- Migrate `System.Drawing` usages to **`SixLabors.ImageSharp`**, already present in the
  solution (`OpenTPW.Files`).
- Find usages: `grep -rn 'System.Drawing\|Bitmap\|Graphics\|Color\b' source/OpenTPW.ModKit`.

## Acceptance criteria

- ModKit launches and shows image viewers on Linux without exceptions.
- The `System.Drawing.Common` package reference is removed.

## Affected files

`source/OpenTPW.ModKit/OpenTPW.ModKit.csproj` + ModKit viewers using `System.Drawing`.
