# Infrastructure and cost

The target application runtime is the existing constrained host, with private
PostgreSQL and persistent mounts; there is no hosted Development environment.
The [runtime contract](workload-consolidation.md) describes the workload and
activation gates. `workload.json` and the platform configuration define the
actual resource limits and placement.

The retained Production Terraform root manages the protected Entra
registration, runtime vault and host access, Cloudflare DNS records, and
protected Google APIs and key. Consult `terraform/` and its Production
variables for the current resources. The reviewed Runtime Infrastructure
workflow plans pull requests and applies approved `main` only on manual
dispatch. It does not deploy the application.

Legacy Azure deployment workflows fail closed. Destruction and retirement are
separate operator-controlled operations; do not infer that a retained script
or workflow authorizes destruction. Preserve protected identity, vault, DNS,
Google project, billing, APIs and keys. See
[retained Google capability](retained-google-capability.md) and the
[runtime contract](workload-consolidation.md) for the narrow Travel federation
handover.

Costs depend on the shared platform and actual retained cloud usage. Consult
the relevant provider cost reports rather than relying on a fixed estimate in
this repository.
