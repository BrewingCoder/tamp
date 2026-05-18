# Security & Compliance Environment Variables

Standardised env-var contract for the `Tamp.Security.*` family of packages
(Wave 1–3 of the Security & Compliance epic, [TAM-234]). Each variable is
read by exactly one satellite; adopters supply values via CI environment,
`.env` files, secret managers, etc.

Per Tamp's universal-not-adopter-specific rule, this doc is the contract —
the satellites never invent variable names locally.

## Naming convention

```
TAMP_<TOOL>_<FIELD>
```

* `TOOL` is the short canonical name of the wrapped service or tool:
  `DT` (Dependency-Track), `DD` (DefectDojo), `OPENGREP`, `TRIVY`,
  `GITLEAKS`, `CHECKOV`, `COSIGN`, `SYFT`.
* `FIELD` is the role: `URL`, `API_KEY`, `TOKEN`, `PROJECT_UUID`,
  `ENGAGEMENT_ID`, `RULES`, `BASELINE`, `IMAGE`, etc.

Values that look like secrets (anything `*_API_KEY`, `*_TOKEN`,
`*_PASSWORD`) are automatically redacted in Tamp logs via
`RedactingTextWriter` if they pass through `Logger`.

## Wave 1 variables

### Dependency-Track ([TAM-241])

| Variable | Required | Description |
|---|---|---|
| `TAMP_DT_URL` | yes | Base URL of the DT instance (e.g. `https://dt.example.com`). No trailing slash. |
| `TAMP_DT_API_KEY` | yes | API key from a DT team with `VIEW_PORTFOLIO` + `VIEW_VULNERABILITY` + `BOM_UPLOAD`. |
| `TAMP_DT_PROJECT_UUID` | yes | UUID of the DT project this build maps to. Per-project; resolve in the build script. |
| `TAMP_DT_ANALYSIS_TIMEOUT` | no | Override the default analysis-completion poll timeout. ISO-8601 duration or seconds (e.g. `PT5M`, `300`). Default: 5 minutes. |

### DefectDojo ([TAM-242])

| Variable | Required | Description |
|---|---|---|
| `TAMP_DD_URL` | yes | Base URL of the DefectDojo instance. No trailing slash. |
| `TAMP_DD_TOKEN` | yes | DefectDojo User API v2 Key. |
| `TAMP_DD_ENGAGEMENT_ID` | yes | Integer ID of the engagement findings get attached to. Per-project. |

### OpenGrep ([TAM-240])

| Variable | Required | Description |
|---|---|---|
| `TAMP_OPENGREP_RULES` | no | Path or registry locator for the rules pack(s) to run. Defaults to OpenGrep's own auto-detection. |
| `TAMP_OPENGREP_BASELINE` | no | Path to a baseline SARIF; only findings absent from this baseline are reported. |

### Tamp.CycloneDx ([TAM-239])

No required env vars — the SBOM producer is fully driven by build-script
settings. Optional overrides may appear in a follow-up.

## Wave 2 variables (placeholders — fill in when those satellites ship)

| Variable | Owner | Status |
|---|---|---|
| `TAMP_SYFT_*` | Tamp.Syft | TBD |
| `TAMP_TRIVY_*` | Tamp.Trivy | TBD |
| `TAMP_GITLEAKS_*` | Tamp.Gitleaks | TBD |
| `TAMP_CHECKOV_*` | Tamp.Checkov | TBD |

## Wave 3 variables (placeholders)

| Variable | Owner | Status |
|---|---|---|
| `TAMP_COSIGN_*` | Tamp.Cosign | TBD |
| `TAMP_SLSA_*` | Tamp.Slsa | TBD |

## Discovery in the build script

Resolve env vars at the boundary (your `TampBuild` class) and pass typed
values into satellite settings. This keeps env-var coupling at the
adopter layer; satellite code reads typed properties, not strings.

```csharp
[Parameter("DT base URL", EnvVar = "TAMP_DT_URL")]
public string DtUrl { get; init; } = "";

[Parameter("DT API key", EnvVar = "TAMP_DT_API_KEY")]
public Secret DtApiKey { get; init; } = Secret.Empty;
```

[TAM-234]: https://yt.brewingcoder.com/issue/TAM-234
[TAM-239]: https://yt.brewingcoder.com/issue/TAM-239
[TAM-240]: https://yt.brewingcoder.com/issue/TAM-240
[TAM-241]: https://yt.brewingcoder.com/issue/TAM-241
[TAM-242]: https://yt.brewingcoder.com/issue/TAM-242
