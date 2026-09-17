#!/usr/bin/env bash
set -euo pipefail

resource_group_name="rg-trip-side-kick-dev-swedencentral"
service_plan_name="asp-trip-side-kick-dev-swedencentral-default"
failure_anomalies_name="Failure Anomalies - ai-trip-side-kick-dev-swedencentral"

failure_anomalies_id="$(az resource list \
  --resource-group "$resource_group_name" \
  --resource-type "Microsoft.AlertsManagement/smartDetectorAlertRules" \
  --query "[?name == '$failure_anomalies_name'].id | [0]" \
  --output tsv)"

if [[ -n "$failure_anomalies_id" ]]; then
  echo "Removing known post-destroy residue: $failure_anomalies_id"
  az resource delete --ids "$failure_anomalies_id"
fi

for attempt in {1..6}; do
  mapfile -t resource_ids < <(az resource list \
    --resource-group "$resource_group_name" \
    --query "[].id" \
    --output tsv)
  mapfile -t service_plan_ids < <(az resource list \
    --name "$service_plan_name" \
    --resource-type "Microsoft.Web/serverfarms" \
    --query "[].id" \
    --output tsv)

  if (( ${#resource_ids[@]} == 0 && ${#service_plan_ids[@]} == 0 )); then
    echo "Verified $resource_group_name is empty and $service_plan_name is absent."
    exit 0
  fi

  if (( attempt < 6 )); then
    sleep 10
  fi
done

echo "::error::Development destroy left Azure resources behind."
printf 'Residual resource ID: %s\n' "${resource_ids[@]}" "${service_plan_ids[@]}" | sort -u
exit 1
