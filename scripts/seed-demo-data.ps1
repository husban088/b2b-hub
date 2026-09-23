<#
  Nexbridge - demo data seeder (fake data only)

  Creates two fake partners (Pantrix, Nexora), their integrations, a few
  webhook events for the live activity feed, and sets varied statuses.
  Safe to run more than once: existing partners/integrations are reused.

  Usage (backend must be running):
    powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-data.ps1
    powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-data.ps1 -ApiBase "https://your-backend.onrender.com"
#>
param(
  [string]$ApiBase = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ApiBase = $ApiBase.TrimEnd('/')
$GraphQlUrl = "$ApiBase/graphql"

function Invoke-Gql {
  param([string]$Query, [hashtable]$Variables = @{})
  $body = ConvertTo-Json -InputObject @{ query = $Query; variables = $Variables } -Depth 10 -Compress
  $res = Invoke-RestMethod -Method Post -Uri $GraphQlUrl -ContentType "application/json" -Body $body
  if ($res.errors) {
    $msg = ($res.errors | ForEach-Object { $_.message }) -join "; "
    throw $msg
  }
  return $res.data
}

function Ensure-Partner {
  param([hashtable]$Partner)

  $data = Invoke-Gql 'query { partners { id companyEmail } }'
  $existing = $data.partners | Where-Object { $_.companyEmail -eq $Partner.companyEmail }

  if ($existing) {
    $id = $existing.id
    Write-Host "  Partner exists: $($Partner.companyName)"
  }
  else {
    $created = Invoke-Gql 'mutation($input: CreatePartnerInput!) { createPartner(input: $input) { id } }' @{
      input = @{
        companyName  = $Partner.companyName
        companyEmail = $Partner.companyEmail
        country      = $Partner.country
        industry     = $Partner.industry
      }
    }
    $id = $created.createPartner.id
    Write-Host "  Partner created: $($Partner.companyName)"
  }

  Invoke-Gql 'mutation($input: UpdatePartnerInput!) { updatePartner(input: $input) { id } }' @{
    input = @{
      id          = $id
      companyName = $Partner.companyName
      country     = $Partner.country
      industry    = $Partner.industry
      status      = "ACTIVE"
    }
  } | Out-Null

  return $id
}

function Ensure-Integration {
  param([string]$PartnerId, [hashtable]$Integration)

  $all = (Invoke-Gql 'query { integrations { id partnerId name } }').integrations
  $hit = $all | Where-Object { $_.partnerId -eq $PartnerId -and $_.name -eq $Integration.name }

  if ($hit) {
    Write-Host "    Integration exists: $($Integration.name)"
    return $hit.id
  }

  $created = Invoke-Gql 'mutation($input: CreateIntegrationInput!) { createIntegration(input: $input) { id } }' @{
    input = @{
      partnerId   = $PartnerId
      name        = $Integration.name
      type        = $Integration.type
      endpointUrl = $Integration.endpointUrl
      scopes      = @($Integration.scopes)
    }
  }
  Write-Host "    Integration created: $($Integration.name)"
  return $created.createIntegration.id
}

function Send-Webhook {
  param([string]$IntegrationId, [string]$EventType, [hashtable]$Payload)
  $body = ConvertTo-Json -InputObject @{ eventType = $EventType; payload = $Payload } -Depth 5 -Compress
  Invoke-RestMethod -Method Post -Uri "$ApiBase/api/webhooks/$IntegrationId" -ContentType "application/json" -Body $body | Out-Null
}

function Set-IntegrationStatus {
  param([string]$IntegrationId, [string]$Status)
  Invoke-Gql 'mutation($id: String!, $status: IntegrationStatus!) { updateIntegrationStatus(id: $id, status: $status) { id } }' @{
    id     = $IntegrationId
    status = $Status
  } | Out-Null
}

try {
  Write-Host "Seeding demo data into $ApiBase ..."

  # ---------------- Pantrix ----------------
  Write-Host "Pantrix"
  $pantrix = Ensure-Partner @{
    companyName  = "Pantrix"
    companyEmail = "partners@pantrix.example.com"
    country      = "Pakistan"
    industry     = "Software and Design"
  }

  $pxOrders = Ensure-Integration $pantrix @{
    name = "Order sync"; type = "REST_API"
    endpointUrl = "https://api.pantrix.example.com/v1/orders"
    scopes = @("orders:read", "orders:write")
  }
  $pxInventory = Ensure-Integration $pantrix @{
    name = "Inventory webhook"; type = "WEBHOOK"
    endpointUrl = "https://hooks.pantrix.example.com/inventory"
    scopes = @("inventory:read")
  }
  $pxCatalog = Ensure-Integration $pantrix @{
    name = "Product catalog feed"; type = "FILE_SYNC"
    endpointUrl = "sftp://files.pantrix.example.com/catalog"
    scopes = @("catalog:read")
  }

  # ---------------- Nexora ----------------
  Write-Host "Nexora"
  $nexora = Ensure-Partner @{
    companyName  = "Nexora"
    companyEmail = "partners@nexora.example.com"
    country      = "Germany"
    industry     = "Logistics"
  }

  $nxInvoices = Ensure-Integration $nexora @{
    name = "Invoice feed"; type = "GRAPHQL"
    endpointUrl = "https://api.nexora.example.com/graphql"
    scopes = @("invoices:read", "invoices:write")
  }
  $nxShipments = Ensure-Integration $nexora @{
    name = "Shipment updates"; type = "WEBHOOK"
    endpointUrl = "https://hooks.nexora.example.com/shipments"
    scopes = @("shipments:read")
  }
  $nxEdi = Ensure-Integration $nexora @{
    name = "Customs EDI"; type = "EDI"
    endpointUrl = "https://edi.nexora.example.com/customs"
    scopes = @("customs:write")
  }

  # ---------------- Activity feed events ----------------
  Write-Host "Sending sample webhook events ..."
  Send-Webhook $pxInventory "stock.updated"   @{ sku = "PX-1042"; quantity = 128 }
  Send-Webhook $pxInventory "stock.low"       @{ sku = "PX-0877"; quantity = 4 }
  Send-Webhook $pxOrders    "order.created"   @{ orderId = "PX-90121"; total = 249.90 }
  Send-Webhook $nxInvoices  "invoice.paid"    @{ invoiceId = "NX-2231"; amount = 1840 }
  Send-Webhook $nxShipments "shipment.sent"   @{ trackingId = "NX-TRK-55821"; carrier = "DHL" }
  Send-Webhook $nxShipments "shipment.delivered" @{ trackingId = "NX-TRK-55790" }

  # One failed outbound event so the feed shows a red dot too.
  Invoke-Gql 'mutation($input: RecordWebhookEventInput!) { recordWebhookEvent(input: $input) { id } }' @{
    input = @{
      integrationId = $nxEdi
      direction     = "outbound"
      eventType     = "customs.declaration"
      statusCode    = 502
      success       = $false
      payload       = '{"declarationId":"NX-DEC-3301"}'
      errorMessage  = "Gateway timeout from partner endpoint"
    }
  } | Out-Null

  # ---------------- Final statuses (set last, webhooks auto-connect) ----------------
  Set-IntegrationStatus $pxOrders    "CONNECTED"
  Set-IntegrationStatus $pxInventory "CONNECTED"
  Set-IntegrationStatus $pxCatalog   "PAUSED"
  Set-IntegrationStatus $nxInvoices  "CONNECTED"
  Set-IntegrationStatus $nxShipments "CONNECTED"
  Set-IntegrationStatus $nxEdi       "FAILING"

  Write-Host ""
  Write-Host "Done. Open the dashboard and check Partners, Integrations and Live activity."
}
catch {
  Write-Host ""
  Write-Host "Seeding failed: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host "Check that the backend is running and reachable at $ApiBase" -ForegroundColor Yellow
  exit 1
}