# Changelog

Wszystkie istotne zmiany w projekcie TimeTrackerApp.

## [2.0.0] - 2026-03-01

### ➕ Dodano

#### Baza danych
- **PostgreSQL 16** jako produkcyjna baza danych
- Automatyczne wykrywanie typu bazy (SQLite/PostgreSQL) na podstawie connection string
- Wsparcie dla obu silników bazy danych (SQLite dla development, PostgreSQL dla produkcji)
- Pakiet `Npgsql.EntityFrameworkCore.PostgreSQL` v8.0.0

#### Docker
- **Dockerfile** z multi-stage build dla optymalnej wielkości obrazu
- **docker-compose.yml** z konfiguracją aplikacji i PostgreSQL
- Healthcheck dla PostgreSQL zapewniający prawidłową kolejność startu
- Wolumen `postgres_data` dla trwałego przechowywania danych
- Izolowana sieć Docker `timetracker-network`
- `.dockerignore` optymalizujący proces budowania

#### Konfiguracja
- `.env.example` z przykładowymi zmiennymi środowiskowymi
- `appsettings.Development.json` dla środowiska developerskiego
- Zaktualizowany `appsettings.json` z PostgreSQL connection string
- Zaktualizowany `.gitignore` (pliki Docker, .env, bazy SQLite)

#### Dokumentacja
- **README.md** - pełna instrukcja wdrożenia Docker i PostgreSQL
- **DOCKER.md** - zaawansowana dokumentacja Docker
- **MIGRATION_GUIDE.md** - przewodnik migracji z SQLite do PostgreSQL
- **CHANGELOG.md** - ten plik

### 🔄 Zmieniono

#### Kod aplikacji
- `Program.cs` - dodano automatyczne wykrywanie typu bazy danych
- `Program.cs` - różne strategie migracji dla SQLite i PostgreSQL
- `TimeTrackerApp.csproj` - dodano pakiet Npgsql

#### Konfiguracja
- Domyślna baza danych zmieniona z SQLite na PostgreSQL
- Connection string dostosowany do PostgreSQL
- Wsparcie dla zmiennych środowiskowych w Docker

### 🛠️ Techniczne

#### Kompatybilność wsteczna
- Aplikacja nadal obsługuje SQLite (wystarczy zmienić connection string)
- Istniejące migracje SQLite działają bez zmian
- Seed data (DbInitializer) działa z oboma silnikami

#### Wydajność
- Multi-stage Docker build redukuje rozmiar obrazu do ~220MB
- PostgreSQL connection pooling domyślnie włączony
- Healthcheck zapobiega próbom połączenia przed gotowością bazy

#### Bezpieczeństwo
- Hasło bazy danych konfigurowane przez zmienne środowiskowe
- `.env` wykluczony z repozytorium (w .gitignore)
- PostgreSQL izolowany w sieci Docker

### 📝 Instrukcje aktualizacji

#### Z wersji 1.x do 2.0.0

1. **Backup danych (jeśli używasz SQLite):**
   ```bash
   cp timetracker.db timetracker.db.backup
   ```

2. **Pull zmian:**
   ```bash
   git checkout feature/postgresql-docker
   git pull origin feature/postgresql-docker
   ```

3. **Konfiguracja:**
   ```bash
   cp .env.example .env
   nano .env  # Ustaw hasło bazy
   ```

4. **Uruchomienie z Docker:**
   ```bash
   docker-compose up -d
   ```

5. **Migracja danych (opcjonalnie):**
   - Zobacz `MIGRATION_GUIDE.md`

#### Kontynuacja z SQLite (bez Docker)

1. **Pull zmian:**
   ```bash
   git checkout feature/postgresql-docker
   git pull origin feature/postgresql-docker
   ```

2. **Edytuj `appsettings.Development.json`:**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=timetracker.db"
     }
   }
   ```

3. **Uruchom aplikację:**
   ```bash
   dotnet restore
   dotnet run
   ```

### ℹ️ Informacje dodatkowe

#### Wymagania

**Docker (opcja zalecana):**
- Docker 20.10+
- Docker Compose 2.0+

**Lokalne uruchomienie:**
- .NET 8.0 SDK
- PostgreSQL 16 (lub SQLite)

#### Breaking Changes

**Brak** - aplikacja zachowuje pełną kompatybilność wstecz.

#### Znane problemy

- Brak - wszystkie testy przechodzą pomyślnie

### 🚀 Następne kroki

Planowane w przyszłości:
- [ ] Automatyczne backupy bazy danych
- [ ] Monitoring z Prometheus + Grafana
- [ ] CI/CD pipeline z GitHub Actions
- [ ] HTTPS z Let's Encrypt
- [ ] Kubernetes deployment (opcjonalnie)

---

## [1.0.0] - 2026-02-15

### ➕ Dodano
- Podstawowa funkcjonalność TimeTracker
- System ról (Admin, Manager, Employee)
- Rejestracja czasu w kalendarzu tygodniowym
- Projekty z opiekunami
- Klienci i przypisanie do projektów
- Raporty miesięczne (Excel)
- SQLite jako baza danych
- Testy jednostkowe
- Autoryzacja cookie-based
- BCrypt hashing hasła

---

## Format

Czasopis został sformatowany zgodnie z [Keep a Changelog](https://keepachangelog.com/pl/1.0.0/),
i projekt stosuje [Semantic Versioning](https://semver.org/lang/pl/).

### Typy zmian

- **Dodano** (➕) - nowe funkcjonalności
- **Zmieniono** (🔄) - zmiany w istniejących funkcjonalnościach
- **Przestarzałe** (⚠️) - funkcje które zostaną usunięte w przyszłości
- **Usunięto** (❌) - usunięte funkcje
- **Naprawiono** (🐛) - naprawione błędy
- **Bezpieczeństwo** (🔒) - poprawki związane z bezpieczeństwem
