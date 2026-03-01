# Przewodnik migracji z SQLite do PostgreSQL

Ten dokument opisuje jak zmigrować istniejące dane z SQLite do PostgreSQL.

## Automatyczna migracja (Zalecane)

Aplikacja automatycznie wykrywa typ bazy danych na podstawie connection string i stosuje odpowiednie migracje.

### Krok 1: Backup danych SQLite

```bash
# Utwórz kopię zapasową bazy SQLite
cp timetracker.db timetracker.db.backup
```

### Krok 2: Export danych do SQL

```bash
# Zainstaluj sqlite3 jeśli nie masz
sudo apt install sqlite3

# Wyeksportuj dane
sqlite3 timetracker.db .dump > sqlite_dump.sql
```

### Krok 3: Przygotuj PostgreSQL

```bash
# Uruchom tylko PostgreSQL z docker-compose
docker-compose up -d postgres

# Poczekaj aż baza będzie gotowa
docker-compose logs -f postgres
# Szukaj: "database system is ready to accept connections"
```

### Krok 4: Konwersja dump SQLite do PostgreSQL

Plik `sqlite_dump.sql` wymaga drobnych modyfikacji dla PostgreSQL:

**Zmiany które należy wykonać:**

1. Usuń/zamień `AUTOINCREMENT` na `SERIAL`
2. Zamień `INTEGER` na `BIGSERIAL` dla kluczy głównych
3. Zamień `TEXT` na `VARCHAR` lub `TEXT` (PostgreSQL obsługuje oba)
4. Zamień `INTEGER NOT NULL DEFAULT 1` na `BOOLEAN DEFAULT TRUE` dla flag
5. Zamień `datetime('now')` na `NOW()`

**Alternatywnie - użyj narzędzia:**

```bash
# Zainstaluj pgloader (konwerter SQLite -> PostgreSQL)
sudo apt install pgloader

# Uruchom konwersję
pgloader timetracker.db postgresql://timetracker_user:TimeTracker2024!@localhost:5432/timetracker
```

### Krok 5: Weryfikacja

```bash
# Połącz się z PostgreSQL
docker-compose exec postgres psql -U timetracker_user -d timetracker

# Sprawdź tabele
\dt

# Sprawdź dane
SELECT COUNT(*) FROM "Users";
SELECT COUNT(*) FROM "Employees";
SELECT COUNT(*) FROM "Projects";
SELECT COUNT(*) FROM "TimeEntries";

# Wyjdź
\q
```

### Krok 6: Uruchom aplikację

```bash
# Uruchom pełną aplikację
docker-compose up -d

# Sprawdź logi
docker-compose logs -f app
```

## Ręczna migracja (Zaawansowane)

### Metoda 1: Użycie pgloader

**Instalacja:**
```bash
# Ubuntu/Debian
sudo apt install pgloader

# macOS
brew install pgloader
```

**Użycie:**
```bash
pgloader sqlite://timetracker.db postgresql://timetracker_user:TimeTracker2024!@localhost:5432/timetracker
```

### Metoda 2: Export/Import przez CSV

**Export z SQLite:**
```bash
sqlite3 timetracker.db <<EOF
.mode csv
.headers on
.output users.csv
SELECT * FROM Users;
.output employees.csv
SELECT * FROM Employees;
.output projects.csv
SELECT * FROM Projects;
.output timeentries.csv
SELECT * FROM TimeEntries;
.quit
EOF
```

**Import do PostgreSQL:**
```bash
# Połącz się z bazą
psql -h localhost -U timetracker_user -d timetracker

# Import danych
\COPY "Users" FROM 'users.csv' CSV HEADER;
\COPY "Employees" FROM 'employees.csv' CSV HEADER;
\COPY "Projects" FROM 'projects.csv' CSV HEADER;
\COPY "TimeEntries" FROM 'timeentries.csv' CSV HEADER;
```

### Metoda 3: Programatyczna migracja

Możesz stworzyć skrypt C# do migracji danych:

```csharp
using Microsoft.EntityFrameworkCore;
using TimeTrackerApp.Data;

// Połącz z SQLite
var sqliteOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlite("Data Source=timetracker.db")
    .Options;

var sqliteDb = new ApplicationDbContext(sqliteOptions);

// Połącz z PostgreSQL
var postgresOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql("Host=localhost;Database=timetracker;Username=timetracker_user;Password=TimeTracker2024!")
    .Options;

var postgresDb = new ApplicationDbContext(postgresOptions);

// Migruj dane
var users = await sqliteDb.Users.ToListAsync();
await postgresDb.Users.AddRangeAsync(users);
await postgresDb.SaveChangesAsync();

// Powtórz dla innych tabel...
```

## Różnice między SQLite a PostgreSQL

### Typy danych

| SQLite | PostgreSQL | Notatki |
|--------|------------|----------|
| INTEGER | BIGINT / SERIAL | Dla ID użyj SERIAL |
| TEXT | VARCHAR / TEXT | PostgreSQL rozróżnia |
| REAL | DOUBLE PRECISION | Liczby zmiennoprzecinkowe |
| BLOB | BYTEA | Dane binarne |
| INTEGER (boolean) | BOOLEAN | Natywny typ boolean |

### Składnia SQL

**SQLite:**
```sql
ALTER TABLE Projects ADD COLUMN ClientId INTEGER;
```

**PostgreSQL:**
```sql
ALTER TABLE "Projects" ADD COLUMN "ClientId" BIGINT;
```

**SQLite:**
```sql
CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL
);
```

**PostgreSQL:**
```sql
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL
);
```

### Cudzysłowy

- **SQLite**: Cudzysłowy opcjonalne
- **PostgreSQL**: Użyj podwójnych cudzysłowów `"TableName"` dla identyfikatorów z wielką literą

### Case sensitivity

- **SQLite**: Case-insensitive (domyślnie)
- **PostgreSQL**: Case-sensitive

## Rozwiązywanie problemów

### Problem: Błędy constraint podczas importu

**Rozwiązanie:**
```sql
-- Tymczasowo wyłącz constrainty
SET session_replication_role = 'replica';

-- Importuj dane
\COPY "Users" FROM 'users.csv' CSV HEADER;

-- Włącz z powrotem
SET session_replication_role = 'origin';
```

### Problem: Sekwencje (SERIAL) nie są zaktualizowane

**Rozwiązanie:**
```sql
-- Zaktualizuj sekwencje po imporcie
SELECT setval('"Users_Id_seq"', (SELECT MAX("Id") FROM "Users"));
SELECT setval('"Employees_Id_seq"', (SELECT MAX("Id") FROM "Employees"));
SELECT setval('"Projects_Id_seq"', (SELECT MAX("Id") FROM "Projects"));
SELECT setval('"TimeEntries_Id_seq"', (SELECT MAX("Id") FROM "TimeEntries"));
```

### Problem: Encoding / znaki specjalne

**Rozwiązanie:**
```bash
# Sprawdź encoding bazy
psql -U timetracker_user -d timetracker -c "SHOW SERVER_ENCODING;"

# Powinno być UTF8
# Jeśli nie, utwórz nową bazę z prawidłowym encoding
CREATE DATABASE timetracker WITH ENCODING 'UTF8';
```

## Testowanie po migracji

### Checklist

- [ ] Wszystkie tabele zostały zmigrowane
- [ ] Liczba rekordów się zgadza (Users, Employees, Projects, TimeEntries)
- [ ] Relacje między tabelami działają (Foreign Keys)
- [ ] Logowanie działa (sprawdź hash hasła)
- [ ] Można tworzyć nowe projekty
- [ ] Można rejestrować czas
- [ ] Raporty generują się poprawnie
- [ ] Brak błędów w logach aplikacji

### Testy SQL

```sql
-- Test 1: Liczba użytkowników
SELECT COUNT(*) as total_users FROM "Users";

-- Test 2: Użytkownicy z pracownikami
SELECT 
    u."Email",
    e."Position",
    u."Role"
FROM "Users" u
INNER JOIN "Employees" e ON u."Id" = e."UserId";

-- Test 3: Projekty z opiekunami
SELECT 
    p."Name" as project_name,
    e."Position" as manager_position,
    u."FirstName" || ' ' || u."LastName" as manager_name
FROM "Projects" p
INNER JOIN "Employees" e ON p."ManagerId" = e."Id"
INNER JOIN "Users" u ON e."UserId" = u."Id";

-- Test 4: Wpisy czasu
SELECT 
    te."Date",
    te."Hours",
    p."Name" as project_name,
    u."FirstName" || ' ' || u."LastName" as employee_name
FROM "TimeEntries" te
LEFT JOIN "Projects" p ON te."ProjectId" = p."Id"
INNER JOIN "Employees" e ON te."EmployeeId" = e."Id"
INNER JOIN "Users" u ON e."UserId" = u."Id"
LIMIT 10;
```

## Backup i Recovery

### Regularne backupy

```bash
# Utwórz skrypt backup.sh
cat > backup.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/path/to/backups"
DATE=$(date +%Y%m%d_%H%M%S)
FILENAME="timetracker_${DATE}.sql"

docker-compose exec -T postgres pg_dump -U timetracker_user timetracker > "${BACKUP_DIR}/${FILENAME}"

# Kompresja
gzip "${BACKUP_DIR}/${FILENAME}"

# Usuń backupy starsze niż 30 dni
find "${BACKUP_DIR}" -name "timetracker_*.sql.gz" -mtime +30 -delete

echo "Backup completed: ${FILENAME}.gz"
EOF

chmod +x backup.sh

# Dodaj do cron (codziennie o 2:00)
crontab -e
0 2 * * * /path/to/backup.sh
```

### Restore z backupu

```bash
# Restore z pliku .sql
docker-compose exec -T postgres psql -U timetracker_user timetracker < backup.sql

# Restore z skompresowanego pliku
gunzip -c backup.sql.gz | docker-compose exec -T postgres psql -U timetracker_user timetracker
```

## Podsumowanie

Po pomyślnej migracji:

1. ✅ Aplikacja działa na PostgreSQL
2. ✅ Wszystkie dane zostały zmigrowane
3. ✅ Backupy są skonfigurowane
4. ✅ Aplikacja działa w Dockerze
5. ✅ Dokumentacja jest zaktualizowana

Gratulacje! Twój TimeTracker jest gotowy do produkcji! 🎉
