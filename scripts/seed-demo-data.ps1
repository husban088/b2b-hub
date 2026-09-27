<#
  Nexbridge - demo data seeder (fake data only)

  Logs in (or registers) a demo company, then creates five fake partners
  (Pantrix, Nexora, BrightForge, Solvara, Kestrel Freight) with their
  integrations, a batch of fake invoices per partner, and a few webhook
  events for the live activity feed. Safe to run more than once: existing
  partners/integrations are reused, and invoices are only created if that
  partner doesn't already have any.

  Usage (backend must be running):
    powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-data.ps1
    powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-data.ps1 -ApiBase "https://your-backend.onrender.com"

  First run with no existing account: pass -Email/-Password/-CompanyName (or
  edit the defaults below) and the script will register that company for you.
  On later runs it just logs in with the same credentials.
#>
param(
  [string]$ApiBase     = "http://localhost:8080",
  [string]$Email       = "demo@nexbridge.example.com",
  [string]$Password    = "DemoPass123!",
  [string]$FullName    = "Demo Admin",
  [string]$CompanyName = "Nexbridge Demo GmbH",
  [string]$VatNumber   = "DE123456789",
  [string]$Iban        = "DE89370400440532013000"
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$ApiBase = $ApiBase.TrimEnd('/')
$GraphQlUrl = "$ApiBase/graphql"
$script:AuthToken = $null
$EmailTag = (($Email -split "@")[0] -replace "[^a-zA-Z0-9]", "").ToLower()
if ([string]::IsNullOrWhiteSpace($EmailTag)) { $EmailTag = "demo" }

function Invoke-Gql {
  param([string]$Query, [hashtable]$Variables = @{})
  $body = ConvertTo-Json -InputObject @{ query = $Query; variables = $Variables } -Depth 10 -Compress
  $headers = @{}
  if ($script:AuthToken) { $headers["Authorization"] = "Bearer $script:AuthToken" }
  try {
    $res = Invoke-RestMethod -Method Post -Uri $GraphQlUrl -ContentType "application/json" -Body $body -Headers $headers
  }
  catch {
    # Surface the real GraphQL/HTTP error body instead of a bare "400 Bad Request".
    $raw = $null
    if ($_.Exception.Response) {
      try {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $raw = $reader.ReadToEnd()
      } catch {}
    }
    if ($raw) { throw "$($_.Exception.Message) - Response body: $raw" }
    else { throw }
  }
  if ($res.errors) {
    $msg = ($res.errors | ForEach-Object { $_.message }) -join "; "
    throw $msg
  }
  return $res.data
}

function Ensure-LoggedIn {
  Write-Host "Authenticating as $Email ..."
  try {
    $data = Invoke-Gql 'mutation($input: LoginInput!) { login(input: $input) { token company { id name } } }' @{
      input = @{ email = $Email; password = $Password }
    }
    $script:AuthToken = $data.login.token
    Write-Host "  Logged in to $($data.login.company.name)"
    return
  }
  catch {
    Write-Host "  Login failed ($($_.Exception.Message)) - trying to register instead ..."
  }

  $data = Invoke-Gql 'mutation($input: RegisterInput!) { register(input: $input) { token company { id name } } }' @{
    input = @{
      fullName    = $FullName
      email       = $Email
      password    = $Password
      companyName = $CompanyName
      vatNumber   = $VatNumber
      iban        = $Iban
    }
  }
  $script:AuthToken = $data.register.token
  Write-Host "  Registered and logged in to $($data.register.company.name)"
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

function Ensure-Invoices {
  param([string]$PartnerId, [string]$PartnerLabel, [array]$Invoices)

  $data = Invoke-Gql 'query { invoices { id partnerId } }'
  $existingCount = ($data.invoices | Where-Object { $_.partnerId -eq $PartnerId }).Count

  if ($existingCount -gt 0) {
    Write-Host "    Invoices exist for $PartnerLabel ($existingCount) - skipping"
    return
  }

  foreach ($inv in $Invoices) {
    try {
      Invoke-Gql 'mutation($input: CreateInvoiceInput!) { createInvoice(input: $input) { id invoiceNumber totalAmount } }' @{
        input = @{
          partnerId   = $PartnerId
          description = $inv.description
          amount      = $inv.amount
        }
      } | Out-Null
      Write-Host "    Invoice created: $($inv.description) (EUR $($inv.amount))"
    }
    catch {
      Write-Host "    Invoice failed: $($inv.description) - $($_.Exception.Message)" -ForegroundColor Yellow
      Write-Host "      (needs a real Stripe test key in appsettings/user-secrets - see .env.example)" -ForegroundColor Yellow
    }
  }
}

try {
  Write-Host "Seeding demo data into $ApiBase ..."
  Ensure-LoggedIn

  # ---------------- Pantrix ----------------
  Write-Host "Pantrix"
  $pantrix = Ensure-Partner @{
    companyName  = "Pantrix"
    companyEmail = "partners@pantrix.$EmailTag.example.com"
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
    companyEmail = "partners@nexora.$EmailTag.example.com"
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

  # ---------------- BrightForge (new) ----------------
  Write-Host "BrightForge"
  $brightforge = Ensure-Partner @{
    companyName  = "BrightForge Manufacturing"
    companyEmail = "partners@brightforge.$EmailTag.example.com"
    country      = "United States"
    industry     = "Manufacturing"
  }

  $bfOrders = Ensure-Integration $brightforge @{
    name = "Purchase order sync"; type = "REST_API"
    endpointUrl = "https://api.brightforge.example.com/v2/purchase-orders"
    scopes = @("orders:read", "orders:write")
  }
  $bfQuality = Ensure-Integration $brightforge @{
    name = "Quality control webhook"; type = "WEBHOOK"
    endpointUrl = "https://hooks.brightforge.example.com/qc"
    scopes = @("quality:read")
  }

  # ---------------- Solvara (new) ----------------
  Write-Host "Solvara"
  $solvara = Ensure-Partner @{
    companyName  = "Solvara Analytics"
    companyEmail = "partners@solvara.$EmailTag.example.com"
    country      = "Canada"
    industry     = "Fintech"
  }

  $svReporting = Ensure-Integration $solvara @{
    name = "Reporting API"; type = "REST_API"
    endpointUrl = "https://api.solvara.example.com/v1/reports"
    scopes = @("reports:read")
  }
  $svLedger = Ensure-Integration $solvara @{
    name = "Ledger sync"; type = "GRAPHQL"
    endpointUrl = "https://api.solvara.example.com/graphql"
    scopes = @("ledger:read", "ledger:write")
  }

  # ---------------- Kestrel Freight (new) ----------------
  Write-Host "Kestrel Freight"
  $kestrel = Ensure-Partner @{
    companyName  = "Kestrel Freight Co"
    companyEmail = "partners@kestrelfreight.$EmailTag.example.com"
    country      = "United Kingdom"
    industry     = "Logistics"
  }

  $kfTracking = Ensure-Integration $kestrel @{
    name = "Fleet tracking webhook"; type = "WEBHOOK"
    endpointUrl = "https://hooks.kestrelfreight.example.com/fleet"
    scopes = @("fleet:read")
  }
  $kfCustoms = Ensure-Integration $kestrel @{
    name = "Customs EDI"; type = "EDI"
    endpointUrl = "https://edi.kestrelfreight.example.com/customs"
    scopes = @("customs:write")
  }

  # ---------------- Invoices (fake, per partner) ----------------
  Write-Host "Creating invoices ..."
  Ensure-Invoices $pantrix "Pantrix" @(
    @{ description = "UI/UX design sprint - Q3 product refresh"; amount = 1450.00 }
    @{ description = "Order sync integration - monthly support"; amount = 320.00 }
    @{ description = "Product catalog feed - setup fee"; amount = 680.00 }
  )
  Ensure-Invoices $nexora "Nexora" @(
    @{ description = "Shipment tracking integration - onboarding"; amount = 2100.00 }
    @{ description = "Customs EDI channel - monthly fee"; amount = 540.00 }
    @{ description = "Invoice feed - Q3 usage"; amount = 975.50 }
  )
  Ensure-Invoices $brightforge "BrightForge" @(
    @{ description = "Purchase order sync - implementation"; amount = 3200.00 }
    @{ description = "Quality control webhook - monthly support"; amount = 410.00 }
    @{ description = "API rate limit upgrade"; amount = 150.00 }
  )
  Ensure-Invoices $solvara "Solvara" @(
    @{ description = "Reporting API integration - onboarding"; amount = 1890.00 }
    @{ description = "Ledger sync - monthly support"; amount = 725.00 }
    @{ description = "Custom compliance report build"; amount = 1120.00 }
  )
  Ensure-Invoices $kestrel "Kestrel Freight" @(
    @{ description = "Fleet tracking webhook - setup"; amount = 980.00 }
    @{ description = "Customs EDI channel - monthly fee"; amount = 560.00 }
    @{ description = "Route optimisation module"; amount = 1340.00 }
  )

  # ---------------- Activity feed events ----------------
  Write-Host "Sending sample webhook events ..."
  Send-Webhook $pxInventory "stock.updated"   @{ sku = "PX-1042"; quantity = 128 }
  Send-Webhook $pxInventory "stock.low"       @{ sku = "PX-0877"; quantity = 4 }
  Send-Webhook $pxOrders    "order.created"   @{ orderId = "PX-90121"; total = 249.90 }
  Send-Webhook $nxInvoices  "invoice.paid"    @{ invoiceId = "NX-2231"; amount = 1840 }
  Send-Webhook $nxShipments "shipment.sent"   @{ trackingId = "NX-TRK-55821"; carrier = "DHL" }
  Send-Webhook $nxShipments "shipment.delivered" @{ trackingId = "NX-TRK-55790" }
  Send-Webhook $bfOrders    "order.created"   @{ orderId = "BF-77410"; total = 3200.00 }
  Send-Webhook $bfQuality   "qc.flagged"      @{ batchId = "BF-BATCH-119"; issue = "tolerance out of range" }
  Send-Webhook $svReporting "report.generated" @{ reportId = "SV-RPT-0042" }
  Send-Webhook $kfTracking  "fleet.location.updated" @{ vehicleId = "KF-VAN-08"; status = "en route" }

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
  Set-IntegrationStatus $pxOrders     "CONNECTED"
  Set-IntegrationStatus $pxInventory  "CONNECTED"
  Set-IntegrationStatus $pxCatalog    "PAUSED"
  Set-IntegrationStatus $nxInvoices   "CONNECTED"
  Set-IntegrationStatus $nxShipments  "CONNECTED"
  Set-IntegrationStatus $nxEdi        "FAILING"
  Set-IntegrationStatus $bfOrders     "CONNECTED"
  Set-IntegrationStatus $bfQuality    "CONNECTED"
  Set-IntegrationStatus $svReporting  "CONNECTED"
  Set-IntegrationStatus $svLedger     "PAUSED"
  Set-IntegrationStatus $kfTracking   "CONNECTED"
  Set-IntegrationStatus $kfCustoms    "CONNECTED"

  Write-Host ""
  Write-Host "Done. 5 partners, their integrations and invoices are seeded."
  Write-Host "Open the dashboard and check Partners, Invoices and Live activity."
}
catch {
  Write-Host ""
  Write-Host "Seeding failed: $($_.Exception.Message)" -ForegroundColor Red
  Write-Host "Check that the backend is running and reachable at $ApiBase" -ForegroundColor Yellow
  exit 1
}