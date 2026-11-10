# Porneste tot sistemul in Kubernetes.
# Rulare din folderul Lab3:  powershell -ExecutionPolicy Bypass -File .\k8s\start.ps1
# Oprire si stergere totala: kubectl delete namespace pad-lab3

$hasApi  = Test-Path ./HotelApi/Dockerfile
$hasSync = Test-Path ./SyncService/SyncService/Dockerfile

Write-Host "`n1. Construiesc imaginile..." -ForegroundColor Cyan
docker build -t gateway:1.0 ./Gateway/Gateway
if ($hasApi)  { docker build -t hotel-api:1.0 ./HotelApi }
if ($hasSync) { docker build -t sync:1.0 ./SyncService/SyncService }

Write-Host "`n2. Namespace pad-lab3..." -ForegroundColor Cyan
kubectl create namespace pad-lab3 --dry-run=client -o yaml | kubectl apply -f -
kubectl config set-context --current --namespace=pad-lab3

Write-Host "`n3. Pornesc Redis, MongoDB si Consul..." -ForegroundColor Cyan
kubectl apply -f k8s/redis.yaml -f k8s/mongo-a.yaml -f k8s/mongo-b.yaml -f k8s/consul.yaml -f k8s/mongo-init.yaml
Write-Host "Astept initializarea replica set-urilor..."
kubectl wait --for=condition=complete job/mongo-init --timeout=180s

Write-Host "`n4. Pornesc aplicatiile..." -ForegroundColor Cyan
kubectl apply -f k8s/gateway.yaml
if ($hasApi)  { kubectl apply -f k8s/api.yaml }  else { Write-Host "HotelApi nu are inca Dockerfile, il sar." -ForegroundColor Yellow }
if ($hasSync) { kubectl apply -f k8s/sync.yaml } else { Write-Host "SyncService nu are inca Dockerfile, il sar." -ForegroundColor Yellow }

Write-Host "`n5. Stare:" -ForegroundColor Cyan
kubectl get pods
Write-Host "`nGateway: http://localhost:5001   Consul: http://localhost:8500" -ForegroundColor Green