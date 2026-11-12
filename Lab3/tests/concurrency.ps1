# Test de concurenta: trimite N cereri POST IN PARALEL prin Gateway.
# Rulare din Lab3:  powershell -ExecutionPolicy Bypass -File .\tests\concurrency.ps1

param([int]$N = 50)

Add-Type -AssemblyName System.Net.Http
$client = New-Object System.Net.Http.HttpClient
$stamp  = Get-Date -Format "HHmmss"

Write-Host "Trimit $N cereri POST in paralel..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# toate cererile pornesc deodata (SendAsync nu asteapta raspunsul)
$tasks = 1..$N | ForEach-Object {
    $json = "{`"name`":`"Concurent $stamp-$_`",`"city`":`"Balti`",`"address`":`"Str. $_`",`"stars`":3,`"totalRooms`":10,`"pricePerNight`":50}"
    $req = New-Object System.Net.Http.HttpRequestMessage 'Post', 'http://localhost:5001/hotels'
    $req.Headers.Add('ClientId', 'premium')   # premium = fara rate limit
    $req.Content = New-Object System.Net.Http.StringContent($json, [Text.Encoding]::UTF8, 'application/json')
    $client.SendAsync($req)
}

# asteptam sa se termine toate
[System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
$sw.Stop()

Write-Host "`nGata in $($sw.ElapsedMilliseconds) ms" -ForegroundColor Green

Write-Host "`nCoduri de raspuns:" -ForegroundColor Cyan
$tasks | Group-Object { [int]$_.Result.StatusCode } | Select-Object @{n="Cod";e={$_.Name}}, Count | Format-Table -AutoSize

Write-Host "Ce instanta a procesat cererile:" -ForegroundColor Cyan
$tasks | Group-Object { $_.Result.Headers.GetValues('X-Instance')[0] } | Select-Object @{n="Instanta";e={$_.Name}}, Count | Format-Table -AutoSize

Write-Host "Verificare in baze (dupa 2 secunde, cat sincronizeaza SyncService):" -ForegroundColor Cyan
Start-Sleep -Seconds 2
$a = docker exec mongo-a1 mongosh hotelsdb --quiet --eval "db.hotels.countDocuments({Name:/^Concurent $stamp-/})"
$b = docker exec mongo-b1 mongosh hotelsdb --quiet --eval "db.hotels.countDocuments({Name:/^Concurent $stamp-/})"
Write-Host "Baza A: $a hoteluri    Baza B: $b hoteluri   (trebuie $N in ambele)"