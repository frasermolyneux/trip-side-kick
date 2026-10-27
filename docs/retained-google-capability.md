# Retained Google Maps capability

The existing `gcp-mx-io-travel-itinerary` project, billing and bootstrap federation
are retained. Trip Production owns Maps/Places enablement and the replacement
key, restricted to those two APIs and the four exact Trip apex/www HTTPS origins.
Both APIs have destruction protection and `disable_on_destroy=false`.
Development has no Google resources; its Azure stack was verified empty first.

The reviewed provider dependency update adds Google 8.8 without upgrading existing
providers. Production PR plans use federated Google auth and redacted output only.
Approved-main apply preserves the generated ADC file by disabling nested checkout.
No project, billing, bootstrap IAM, old key or old trust is removed here.

After the reviewed main-only Runtime Infrastructure apply, dispatch
`deliver-google-maps.yml`. It verifies live API enablements and exact key metadata,
then publishes the key through the Azure management-plane API to the retained
default-deny vault. It does not print the value, create Terraform secret outputs,
or upload plans/state. Repeated delivery preserves an unchanged secret version.
The host can retrieve it through its existing scoped vault role; no application
feature or additional runtime mount is invented because Trip has no Maps consumer.

Only after secure delivery and actual API acceptance may Travel execute its
guarded normal-apply forget handover for **both** legacy states. `plan -destroy`
ignores `removed` protection, so neither legacy destroy may precede that handover.
Keep old keys/trust until legacy stack operations finish, then revoke them without
deleting the retained project or disabling APIs. Follow Travel's decommissioning
runbook and the platform catalog's state-first retirement instructions.
