<#
Simple host-side update agent (PowerShell)
Usage:
  pwsh ./UpdateAgent.ps1 -Token "my-secret" -Port 8080

This listens on http://127.0.0.1:<port>/ and accepts POST /update.
Request headers:
  X-Update-Token: <token>
Body (JSON):
  { "service": "my-service", "composeFile": "docker-compose.yml" }
Or for watchtower run-once:
  { "watchtowerContainer": "my-container" }

Security: bind to localhost and require a secret token. Run this as a service/user-managed process on the Docker host.
#>
param(
    [string]$Token = $env:UPDATE_AGENT_TOKEN,
    [int]$Port = 8080
)

if (-not $Token) {
    Write-Error "No token provided. Set -Token or UPDATE_AGENT_TOKEN environment variable. Exiting."
    exit 2
}

$prefix = "http://127.0.0.1:$Port/"
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($prefix)
$listener.Start()
Write-Host "Update agent listening on $prefix" -ForegroundColor Green

function Write-JsonResponse($context, $statusCode, $obj) {
    $json = $obj | ConvertTo-Json -Depth 5
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $context.Response.StatusCode = $statusCode
    $context.Response.ContentType = 'application/json'
    $context.Response.ContentLength64 = $bytes.Length
    $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $context.Response.OutputStream.Close()
}

while ($true) {
    try {
        $ctx = $listener.GetContext()
        # Only accept requests from localhost. HttpListener bound to 127.0.0.1 should enforce this, but check IP.
        $remote = $ctx.Request.RemoteEndPoint.Address.ToString()
        if ($remote -ne '127.0.0.1' -and $remote -ne '::1') {
            Write-Warning "Rejected connection from $remote"
            Write-JsonResponse $ctx 403 @{ error = 'Forbidden' }
            continue
        }

        $path = $ctx.Request.Url.AbsolutePath
        if ($ctx.Request.HttpMethod -ne 'POST' -or $path -ne '/update') {
            Write-JsonResponse $ctx 404 @{ error = 'Not found' }
            continue
        }

        $provided = $ctx.Request.Headers['X-Update-Token']
        if ($provided -ne $Token) {
            Write-Warning "Unauthorized request"
            Write-JsonResponse $ctx 401 @{ error = 'Unauthorized' }
            continue
        }

        $reader = New-Object System.IO.StreamReader($ctx.Request.InputStream, $ctx.Request.ContentEncoding)
        $body = $reader.ReadToEnd()
        $reader.Close()

        if (-not $body) {
            Write-JsonResponse $ctx 400 @{ error = 'Empty body' }
            continue
        }

        try { $payload = $body | ConvertFrom-Json } catch { Write-JsonResponse $ctx 400 @{ error = 'Invalid JSON'; detail = $_.Exception.Message }; continue }

        # Two supported modes: watchtower run-once or docker compose pull/up
        if ($null -ne $payload.watchtowerContainer) {
            $container = $payload.watchtowerContainer
            Write-Host "Running watchtower (run-once) for container: $container"
            try {
                $cmd = "docker run --rm containrrr/watchtower --run-once $container"
                Write-Host "Executing: $cmd"
                $proc = Start-Process -FilePath 'docker' -ArgumentList @('run','--rm','containrrr/watchtower','--run-once',$container) -NoNewWindow -Wait -PassThru -RedirectStandardOutput 'std_out.log' -RedirectStandardError 'std_err.log'
                $out = Get-Content -Raw 'std_out.log' -ErrorAction SilentlyContinue
                $err = Get-Content -Raw 'std_err.log' -ErrorAction SilentlyContinue
                Write-JsonResponse $ctx 200 @{ status = 'ok'; mode = 'watchtower'; stdout = $out; stderr = $err }
            } catch {
                Write-JsonResponse $ctx 500 @{ error = 'watchtower failed'; detail = $_.Exception.Message }
            } finally {
                Remove-Item -ErrorAction SilentlyContinue 'std_out.log','std_err.log'
            }
            continue
        }

        $service = $payload.service
        $composeFile = $payload.composeFile
        if (-not $service) {
            Write-JsonResponse $ctx 400 @{ error = 'service is required' }
            continue
        }

        # Build docker compose args
        $composeArgs = @('compose')
        if ($composeFile) { $composeArgs += ('-f', $composeFile) }

        # docker compose pull <service>
        try {
            Write-Host "Pulling image for service: $service"
            $pullArgs = $composeArgs + @('pull', $service)
            Write-Host "Executing: docker $($pullArgs -join ' ')"
            $pull = Start-Process -FilePath 'docker' -ArgumentList $pullArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput 'pull_out.log' -RedirectStandardError 'pull_err.log'
            $pullOut = Get-Content -Raw 'pull_out.log' -ErrorAction SilentlyContinue
            $pullErr = Get-Content -Raw 'pull_err.log' -ErrorAction SilentlyContinue

            Write-Host "Starting service: $service"
            $upArgs = $composeArgs + @('up','-d',$service)
            Write-Host "Executing: docker $($upArgs -join ' ')"
            $up = Start-Process -FilePath 'docker' -ArgumentList $upArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput 'up_out.log' -RedirectStandardError 'up_err.log'
            $upOut = Get-Content -Raw 'up_out.log' -ErrorAction SilentlyContinue
            $upErr = Get-Content -Raw 'up_err.log' -ErrorAction SilentlyContinue

            Write-JsonResponse $ctx 200 @{ status = 'ok'; service = $service; pull_stdout = $pullOut; pull_stderr = $pullErr; up_stdout = $upOut; up_stderr = $upErr }
        } catch {
            Write-JsonResponse $ctx 500 @{ error = 'pull/up failed'; detail = $_.Exception.Message }
        } finally {
            Remove-Item -ErrorAction SilentlyContinue 'pull_out.log','pull_err.log','up_out.log','up_err.log'
        }

    } catch {
        Write-Warning "Listener exception: $_"
    }
}
