# TimeTrackerApp

System do rejestracji czasu pracy pracowników w projektach.

## Funkcjonalności

### Zarządzanie użytkownikami i pracownikami
- Trzystopniowy system ról: **Admin**, **Manager** (Kierownik), **Employee** (Pracownik)
- Panel zarządzania pracownikami (dostarcza Admin i Manager)
- Profile pracowników z informacjami: stanowisko, departament, data zatrudnienia

### Projekty
- Tworzenie i edycja projektów
- **Opiekun projektu** - każdy projekt musi mieć przypisanego Kierownika jako opiekuna
- Przypisywanie pracowników do projektów (relacja wiele-do-wielu)
- Budżet godzinowy i monitorowanie jego wykorzystania
- Statusy projektów: Aktywny, Planowanie, Wstrzymany, Zakończony
- Walidacja: pracownicy mogą rejestroć czas tylko w przypisanych im projektach

### Rejestracja czasu
- **Kalendarz tygodniowy** (widok siatki) - rejestracja czasu z dokładnością do minut
- Dodawanie wpisów czasu bezpośrednio w kalendarzu (AJAX)
- Edycja i usuwanie wpisów czasu
- Przydział wpisu do projektu (opcjonalnie)
- Markery dni: Urlop, Choroba, Urlop okolicznościowy, Szkolenie
- Pracownicy widzą tylko swój kalendarz; Admin/Manager widzą wszystkich

### Raporty
- **Raport miesięczny** (Excel) - szczegółowy czas pracy dla każdego pracownika
- Eksport do formatu Excel (.xlsx)
- Filtrowanie raportów po miesiącu i pracowniku

### Zabezpieczenia
- Autoryzacja oparta na rolach
- Haszowanie haseł (BCrypt)
- Walidacja po stronie serwera
- Pracownicy mają dostęp tylko do swoich danych

## Technologie

- **Backend**: ASP.NET Core 8.0 (MVC)
- **Baza danych**: PostgreSQL 16 (produkcja) / SQLite (development)
- **Konteneryzacja**: Docker + Docker Compose
- **ORM**: Entity Framework Core
- **Frontend**: Razor Views, CSS (custom), JavaScript (vanilla)
- **Eksport**: EPPlus (Excel)

## Instalacja i uruchomienie

### Opcja 1: Docker (Zalecane dla produkcji)

#### Wymagania
- Docker 20.10+
- Docker Compose 2.0+

#### Kroki

1. **Klonowanie repozytorium:**
```bash
git clone https://github.com/pantig/TimeTrackerWSB.git
cd TimeTrackerWSB
```

2. **Konfiguracja zmiennych środowiskowych:**
```bash
cp .env.example .env
# Edytuj .env i ustaw hasło do bazy danych
nano .env
```

3. **Uruchomienie aplikacji:**
```bash
docker-compose up -d
```

4. **Sprawdź status kontenerów:**
```bash
docker-compose ps
```

5. **Aplikacja dostępna pod adresem:**
```
http://localhost:5000
```

#### Zarządzanie Docker

**Zatrzymanie aplikacji:**
```bash
docker-compose down
```

**Zatrzymanie z usunięciem danych:**
```bash
docker-compose down -v
```

**Podgląd logów:**
```bash
docker-compose logs -f app
docker-compose logs -f postgres
```

**Restart aplikacji:**
```bash
docker-compose restart app
```

**Przebudowanie obrazu po zmianach w kodzie:**
```bash
docker-compose up -d --build
```

#### Połączenie z bazą PostgreSQL

**Z hosta (localhost):**
```bash
psql -h localhost -p 5432 -U timetracker_user -d timetracker
```

**Z kontenera aplikacji:**
```bash
docker-compose exec app psql -h postgres -U timetracker_user -d timetracker
```

**Backup bazy danych:**
```bash
docker-compose exec postgres pg_dump -U timetracker_user timetracker > backup.sql
```

**Restore bazy danych:**
```bash
docker-compose exec -T postgres psql -U timetracker_user timetracker < backup.sql
```

### Opcja 2: Lokalne uruchomienie (Development)

#### Wymagania
- .NET 8.0 SDK
- PostgreSQL 16 (lub SQLite dla szybkich testów)

#### Kroki z PostgreSQL

1. **Zainstaluj PostgreSQL:**
```bash
# Ubuntu/Debian
sudo apt install postgresql-16

# Windows - pobierz installer z postgresql.org
# macOS
brew install postgresql@16
```

2. **Utwórz bazę danych:**
```bash
sudo -u postgres psql
CREATE DATABASE timetracker;
CREATE USER timetracker_user WITH PASSWORD 'TimeTracker2024!';
GRANT ALL PRIVILEGES ON DATABASE timetracker TO timetracker_user;
\q
```

3. **Klonowanie repozytorium:**
```bash
git clone https://github.com/pantig/TimeTrackerWSB.git
cd TimeTrackerWSB
```

4. **Edycja connection string (opcjonalnie):**
```bash
nano appsettings.Development.json
# Dostosuj hasło jeśli zmieniłeś je w PostgreSQL
```

5. **Przywracanie pakietów:**
```bash
dotnet restore
```

6. **Uruchomienie aplikacji:**
```bash
dotnet run
```

7. **Otwórz przeglądarkę:**
```
http://localhost:5000
```

#### Kroki z SQLite (szybki development)

1. **Zmień connection string w `appsettings.Development.json`:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=timetracker.db"
  }
}
```

2. **Uruchom aplikację:**
```bash
dotnet run
```

### Domyślne konta (seed data)

Po pierwszym uruchomieniu system tworzy konta testowe:

```
Admin: admin@test.com / Admin123!
Manager: manager@test.com / Manager123!
Employee: employee@test.com / Employee123!
```

## Struktura projektu

```
TimeTrackerWSB/
├── Controllers/          # Kontrolery MVC
├── Data/                 # Kontekst bazy danych
├── Migrations/           # Migracje bazy danych
├── Models/               # Modele domen
├── Repositories/         # Warstwa dostępu do danych
├── Services/             # Logika biznesowa
├── Views/                # Widoki Razor
├── wwwroot/              # Pliki statyczne (CSS, JS)
├── TimeTrackerApp.Tests/ # Testy jednostkowe
├── docker-compose.yml    # Konfiguracja Docker
├── Dockerfile            # Obraz Docker aplikacji
└── Program.cs            # Punkt wejścia aplikacji
```

## Konfiguracja produkcyjna

### Zmienne środowiskowe

Dla środowiska produkcyjnego ustaw:

```bash
# W pliku .env
DB_PASSWORD=<silne_hasło_produkcyjne>
ASPNETCORE_ENVIRONMENT=Production
```

### Bezpieczeństwo

1. **Zmień hasło bazy danych** w `.env`
2. **Ustaw HTTPS** (wymaga certyfikatu SSL)
3. **Skonfiguruj firewall** - ogranicz dostęp do portu 5432 PostgreSQL
4. **Regularne backupy bazy danych**
5. **Monitoring logów** - sprawdzaj logi aplikacji i PostgreSQL

### Wydajność

Dla większego obciążenia rozważ:

1. **Connection pooling** - domyślnie włączony w Npgsql
2. **Indeksy bazy danych** - już skonfigurowane dla kluczowych kolumn
3. **Caching** - rozważ Redis dla sesji użytkowników
4. **Load balancing** - dla wielu instancji aplikacji

## Rozwiązywanie problemów

### Problem: Kontener nie może połączyć się z PostgreSQL

**Rozwiązanie:**
```bash
# Sprawdź status
docker-compose ps

# Sprawdź logi PostgreSQL
docker-compose logs postgres

# Zrestartuj usługę
docker-compose restart postgres
```

### Problem: Migracje nie działają

**Rozwiązanie:**
```bash
# Wejdź do kontenera aplikacji
docker-compose exec app bash

# Sprawdź connection string
echo $ConnectionStrings__DefaultConnection

# Sprawdź status bazy
psql -h postgres -U timetracker_user -d timetracker -c "SELECT version();"
```

### Problem: Port 5432 już zajęty

**Rozwiązanie:**
```bash
# Zmień port w docker-compose.yml
ports:
  - "5433:5432"  # Zewnętrzny port 5433

# Zrestartuj
docker-compose up -d
```

## Licencja

Projekt edukacyjny - WSB

## Kontakt

W razie pytań - utwórz issue na GitHub.
