# T-005 — Dependencies with known vulnerabilities

- **Priority**: 🟠 Medium
- **Type**: Security / maintenance
- **Confirmed by**: NuGet warnings at build time

## Findings

```
NU1903 (high)     SixLabors.ImageSharp 3.1.6  → GHSA-2cmq-823j-5qj8
NU1902 (moderate) SixLabors.ImageSharp 3.1.6  → GHSA-rxmq-m78w-7wmc
NU1901 (low)      Zio 0.17.0                  → GHSA-h39g-6x3c-7fq9
```

## Proposed fix

- **ImageSharp**: bump to a fixed `3.1.x` (≥ 3.1.11 / latest 3.1.x) — check the
  advisory changelogs. High priority because of the **high** severity.
- **Zio**: bump to a fixed version if available (low severity, less urgent).
- Run `dotnet list package --vulnerable` after the update to validate.

## Acceptance criteria

- `dotnet list package --vulnerable` reports no more high/moderate findings.
- Build still green, tests still passing.

## Affected files

`source/OpenTPW.Files/OpenTPW.Files.csproj`, `source/OpenTPW.Common/OpenTPW.Common.csproj`
(and any `.csproj` referencing ImageSharp/Zio).
