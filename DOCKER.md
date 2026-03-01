# Docker - Przewodnik

Szczegółowa dokumentacja wdrożenia Docker dla TimeTrackerApp.

## Architektura

### Kontenery

```
┌────────────────────────────┐
│   timetracker-app          │
│   (ASP.NET Core 8.0)       │
│   Port: 5000               │
└─────────┬──────────────────┘
         │
         │ timetracker-network
         │
┌────────┴──────────────────┐
│   timetracker-postgres     │
│   (PostgreSQL 16)          │
│   Port: 5432               │
│   Volume: postgres_data    │
└────────────────────────────┘
```

### Sieć

- **Network**: `timetracker-network` (bridge)
- **Komunikacja**: Kontenery komunikują się przez nazwy serwisów (`postgres`, `app`)
- **Izolacja**: Sieć izolowana od innych aplikacji Docker

### Wolumeny

- **postgres_data**: Trwałe przechowywanie danych PostgreSQL
- **Lokalizacja**: `/var/lib/docker/volumes/timetracker_postgres_data`

## Dockerfile - Szczegóły

### Multi-stage build

```dockerfile
# Stage 1: Build (SDK image)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# - Pełne środowisko .NET SDK
# - Zawiera kompilator, narzędzia budowania
# - Rozmiar: ~1.2GB

# Stage 2: Publish
FROM build AS publish
# - Tworzy zoptymalizowaną wersję aplikacji
# - Usuwa zbyteczne pliki

# Stage 3: Runtime (Runtime image)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
# - Tylko środowisko uruchomieniowe ASP.NET
# - Brak kompilatora i narzędzi build
# - Rozmiar: ~220MB
```

**Korzyści:**
- Mały finalny obraz (~220MB vs ~1.2GB)
- Szybsze deployments
- Mniejsza powierzchnia ataku (security)
- Brak niepotrzebnych narzędzi w produkcji

### Warstwy (Layers)

```dockerfile
COPY ["TimeTrackerApp.csproj", "./"]  # Layer 1
RUN dotnet restore                      # Layer 2 (cache)
COPY . .                                # Layer 3
RUN dotnet build                        # Layer 4
```

**Optymalizacja cache:**
- Jeśli `TimeTrackerApp.csproj` się nie zmienił, warstwy 1-2 są użyte z cache
- Przyspiesza rebuildy przy zmianach tylko w kodzie

## docker-compose.yml - Szczegóły

### Zmienne środowiskowe

```yaml
environment:
  - ConnectionStrings__DefaultConnection=Host=postgres;...
```

**Format**: `Section__Key=Value` (ASP.NET Core convention)

**Nadpisanie w .env:**
```bash
DB_PASSWORD=MojeSilneHaslo123!
```

### Healthcheck

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U timetracker_user -d timetracker"]
  interval: 10s
  timeout: 5s
  retries: 5
```

**Działanie:**
1. Co 10 sekund sprawdza czy PostgreSQL jest gotowy
2. Timeout: 5 sekund na odpowiedź
3. Po 5 nieudanych próbach - kontener uznany za unhealthy

**Użycie:**
```yaml
app:
  depends_on:
    postgres:
      condition: service_healthy  # Czeka aż healthcheck przejdzie
```

### Restart policies

```yaml
restart: unless-stopped
```

**Opcje:**
- `no` - Nigdy nie restartuj
- `always` - Zawsze restartuj (nawet po reboot hosta)
- `on-failure` - Tylko przy błędzie (exit code != 0)
- `unless-stopped` - Zawsze, chyba że manualnie zatrzymany

## Komendy Docker

### Podstawowe operacje

```bash
# Build i uruchomienie
docker-compose up -d --build

# Tylko uruchomienie (bez build)
docker-compose up -d

# Zatrzymanie
docker-compose stop

# Zatrzymanie + usunięcie kontenerów
docker-compose down

# Zatrzymanie + usunięcie kontenerów + wolumenów
docker-compose down -v
```

### Diagnostyka

```bash
# Status kontenerów
docker-compose ps

# Logi (real-time)
docker-compose logs -f

# Logi konkretnego serwisu
docker-compose logs -f app
docker-compose logs -f postgres

# Ostatnie 100 linii
docker-compose logs --tail=100 app

# Inspekcja kontenera
docker inspect timetracker-app

# Statystyki zasobów
docker stats timetracker-app timetracker-postgres
```

### Interakcja z kontenerami

```bash
# Shell w kontenerze aplikacji
docker-compose exec app bash

# Shell w kontenerze PostgreSQL
docker-compose exec postgres bash

# Jednorazowa komenda
docker-compose exec app dotnet --version
docker-compose exec postgres psql -U timetracker_user -d timetracker

# Kopiowanie plików
docker cp timetracker-app:/app/logs ./logs
```

### Czyszczenie

```bash
# Usunięcie nieaktywnych kontenerów
docker container prune

# Usunięcie nieaktywnych obrazów
docker image prune

# Usunięcie nieaktywnych wolumenów
docker volume prune

# Usunięcie wszystkiego nieaktywnego
docker system prune -a --volumes
```

## Optymalizacja wydajności

### 1. Resource limits

Dodaj do `docker-compose.yml`:

```yaml
services:
  app:
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 512M
        reservations:
          cpus: '0.5'
          memory: 256M

  postgres:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
```

### 2. PostgreSQL tuning

Dodaj do `docker-compose.yml`:

```yaml
postgres:
  command:
    - postgres
    - -c
    - shared_buffers=256MB
    - -c
    - effective_cache_size=1GB
    - -c
    - maintenance_work_mem=128MB
    - -c
    - checkpoint_completion_target=0.9
    - -c
    - wal_buffers=16MB
    - -c
    - default_statistics_target=100
    - -c
    - random_page_cost=1.1
    - -c
    - effective_io_concurrency=200
    - -c
    - work_mem=4MB
    - -c
    - min_wal_size=1GB
    - -c
    - max_wal_size=4GB
```

### 3. Caching warstw Docker

Użyj `.dockerignore` aby wykluczyć niepotrzebne pliki:

```
bin/
obj/
*.db
.git/
README.md
```

## Monitoring

### Podstawowy monitoring

```bash
# Skrypt monitoring.sh
cat > monitoring.sh << 'EOF'
#!/bin/bash

while true; do
  clear
  echo "=== TimeTracker Docker Status ==="
  echo ""
  
  echo "Containers:"
  docker-compose ps
  echo ""
  
  echo "Resource Usage:"
  docker stats --no-stream timetracker-app timetracker-postgres
  echo ""
  
  echo "Disk Usage:"
  docker system df
  echo ""
  
  echo "Last 5 app logs:"
  docker-compose logs --tail=5 app
  
  sleep 5
done
EOF

chmod +x monitoring.sh
./monitoring.sh
```

### Zaawansowany monitoring - Prometheus + Grafana

Dodaj do `docker-compose.yml`:

```yaml
services:
  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus_data:/prometheus
    ports:
      - "9090:9090"
    networks:
      - timetracker-network

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana_data:/var/lib/grafana
    networks:
      - timetracker-network

volumes:
  prometheus_data:
  grafana_data:
```

## Bezpieczeństwo

### 1. Nie commituj secrets

```bash
# Dodaj do .gitignore
echo ".env" >> .gitignore
echo "*.key" >> .gitignore
echo "*.pem" >> .gitignore
```

### 2. Użyj Docker secrets (Swarm)

```yaml
secrets:
  db_password:
    external: true

services:
  postgres:
    secrets:
      - db_password
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
```

### 3. Skanowanie obrazu

```bash
# Zainstaluj trivy
sudo apt install wget apt-transport-https gnupg lsb-release
wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
echo "deb https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee /etc/apt/sources.list.d/trivy.list
sudo apt update
sudo apt install trivy

# Skanuj obraz
trivy image timetracker-app:latest
```

### 4. Non-root user

Dodaj do `Dockerfile`:

```dockerfile
# Utwórz non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Zmień właściciela plików
RUN chown -R appuser:appuser /app

# Przejdź na non-root user
USER appuser

ENTRYPOINT ["dotnet", "TimeTrackerApp.dll"]
```

## Backup i Recovery

### Automatyczny backup

```bash
# Skrypt backup.sh
cat > backup.sh << 'EOF'
#!/bin/bash

BACKUP_DIR="/backups/timetracker"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "${BACKUP_DIR}"

# Backup PostgreSQL
docker-compose exec -T postgres pg_dump -U timetracker_user timetracker | gzip > "${BACKUP_DIR}/db_${DATE}.sql.gz"

# Backup wolumenu
docker run --rm -v timetracker_postgres_data:/data -v "${BACKUP_DIR}":/backup alpine tar czf /backup/volume_${DATE}.tar.gz -C /data .

# Usuwanie starych backupów (>30 dni)
find "${BACKUP_DIR}" -name "*.gz" -mtime +30 -delete

echo "Backup completed: ${DATE}"
EOF

chmod +x backup.sh

# Dodaj do crontab
(crontab -l 2>/dev/null; echo "0 2 * * * /path/to/backup.sh") | crontab -
```

### Restore

```bash
# Restore bazy danych
gunzip -c db_20260301_020000.sql.gz | docker-compose exec -T postgres psql -U timetracker_user timetracker

# Restore wolumenu
docker run --rm -v timetracker_postgres_data:/data -v /backups/timetracker:/backup alpine sh -c "cd /data && tar xzf /backup/volume_20260301_020000.tar.gz"
```

## CI/CD Integration

### GitHub Actions - Przykład

```yaml
# .github/workflows/docker.yml
name: Docker Build and Deploy

on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Build Docker image
        run: docker build -t timetracker:${{ github.sha }} .
      
      - name: Run tests
        run: docker-compose -f docker-compose.test.yml up --abort-on-container-exit
      
      - name: Push to registry
        run: |
          echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
          docker push timetracker:${{ github.sha }}
```

## Troubleshooting

### Problem: "Port already in use"

```bash
# Sprawdź który proces używa portu
sudo lsof -i :5000
sudo lsof -i :5432

# Zmień port w docker-compose.yml
ports:
  - "5001:5000"  # Zewnętrzny:Wewnętrzny
```

### Problem: "No space left on device"

```bash
# Sprawdź zużycie Dockera
docker system df

# Wyczyść nieaktywne zasoby
docker system prune -a --volumes

# Zwiększ przestrzeń dla Docker
# Linux: /var/lib/docker
# Windows: Settings -> Resources -> Disk image size
```

### Problem: Kontener ciągle restartuje

```bash
# Sprawdź logi
docker-compose logs app

# Sprawdź exit code
docker ps -a | grep timetracker-app

# Wyłącz auto-restart i uruchom ręcznie
docker-compose up app  # Bez -d
```

### Problem: Wolne buildy

```bash
# Użyj BuildKit
export DOCKER_BUILDKIT=1
docker-compose build

# Cache from registry
docker build --cache-from timetracker:latest -t timetracker:new .
```

## Podsumowanie

### Checklist wdrożenia

- [ ] Docker i Docker Compose zainstalowane
- [ ] Plik `.env` skonfigurowany
- [ ] Aplikacja buduje się bez błędów
- [ ] PostgreSQL startuje poprawnie
- [ ] Aplikacja łączy się z bazą
- [ ] Healthchecks działają
- [ ] Backupy skonfigurowane
- [ ] Monitoring uruchomiony (opcjonalnie)
- [ ] Dokumentacja zaktualizowana

### Przydatne linki

- [Docker Documentation](https://docs.docker.com/)
- [Docker Compose Reference](https://docs.docker.com/compose/compose-file/)
- [PostgreSQL Docker Image](https://hub.docker.com/_/postgres)
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
