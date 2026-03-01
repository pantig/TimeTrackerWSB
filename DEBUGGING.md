# Debugging Guide - Problemy z bazą danych

## Problem: Brak seed data lub użytkownicy nie zapisują się

### 1. Sprawdź logi aplikacji

```bash
# Zobacz logi aplikacji
docker-compose logs app

# Śledź logi na bieżąco
docker-compose logs -f app

# Szukaj konkretnych komunikatów
docker-compose logs app | grep "ERROR"
docker-compose logs app | grep "seed data"
docker-compose logs app | grep "User created"
```

**Czego szukać:**
- `[INFO] Using PostgreSQL database provider`
- `[INFO] Can connect to database: True`
- `[INFO] Seed data created` lub `[INFO] Seed data already exists`
- `[INFO] Verifying database contents:`
- Błędy typu `Cannot connect to database`
- Błędy typu `DbUpdateException`

### 2. Sprawdź połączenie z PostgreSQL

```bash
# Sprawdź czy PostgreSQL działa
docker-compose ps

# Powinno pokazać:
# postgres   Up   5432/tcp
# app        Up   0.0.0.0:5000->5000/tcp

# Połącz się z bazą bezpośrednio
docker-compose exec postgres psql -U timetracker_user -d timetracker
```

### 3. Sprawdź zawartość bazy danych

Po połączeniu z psql:

```sql
-- Sprawdź tabele
\dt

-- Sprawdź użytkowników
SELECT "Id", "Email", "FirstName", "LastName", "Role", "IsActive" 
FROM "Users";

-- Sprawdź pracowników
SELECT "Id", "UserId", "Position", "Department" 
FROM "Employees";

-- Sprawdź projekty
SELECT "Id", "Name", "Status" 
FROM "Projects";

-- Zakończ
\q
```

### 4. Testowanie rejestracji użytkownika

**Otwarte w drugiej konsoli (przed rejestracją):**
```bash
# Otwieramy logi w trybie ciągłym
docker-compose logs -f app
```

**Rejestruj użytkownika przez przeglądarkę:**
1. Otwórz `http://localhost:5000/Account/Register`
2. Wypełnij formularz
3. Kliknij "Zarejestruj się"

**W logach powinny pojawić się:**
```
[INFO] Creating new user: test@example.com
[INFO] User created successfully. Changes saved: 1
[INFO] User verified in database. ID: 4
```

**Jeśli widoczne błędy:**
```
[ERROR] Database error during user registration: test@example.com
```

### 5. Reset bazy danych (pełny restart)

```bash
# Zatrzymaj kontenery
docker-compose down

# Usuń dane PostgreSQL (UWAGA: usuwa całą bazę!)
rm -rf postgres-data/

# Uruchom ponownie
docker-compose up -d

# Sprawdź logi inicjalizacji
docker-compose logs app | grep "seed data"
```

### 6. Weryfikacja connection string

```bash
# Sprawdź connection string w kontenerze
docker-compose exec app printenv | grep ConnectionStrings

# Powinno być:
# ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;...
```

**Ważne:** Connection string MUSI wskazać na `Host=postgres` (nazwa serwisu Docker), nie `localhost`!

### 7. Sprawdzanie migracji

```bash
# Wejdź do kontenera aplikacji
docker-compose exec app bash

# Sprawdź zastosowane migracje
dotnet ef migrations list

# Wyjdź
exit
```

### 8. Manualne dodanie seed data

Jeśli seed data nie został utworzony automatycznie:

```bash
# Połącz się z bazą
docker-compose exec postgres psql -U timetracker_user -d timetracker
```

```sql
-- Dodaj użytkowników manualnie
INSERT INTO "Users" ("Id", "Email", "PasswordHash", "FirstName", "LastName", "Role", "IsActive")
VALUES 
  (gen_random_uuid(), 'admin@test.com', '$2a$11$yourhashedpassword', 'Admin', 'System', 0, true),
  (gen_random_uuid(), 'manager@test.com', '$2a$11$yourhashedpassword', 'Jan', 'Kierownik', 1, true),
  (gen_random_uuid(), 'employee@test.com', '$2a$11$yourhashedpassword', 'Piotr', 'Pracownik', 2, true);
```

### 9. Typowe problemy

#### Problem: "Cannot connect to database"
**Rozwiązanie:**
```bash
# Sprawdź czy PostgreSQL działa
docker-compose ps postgres

# Jeśli nie działa, zrestartuj
docker-compose restart postgres

# Sprawdź logi PostgreSQL
docker-compose logs postgres
```

#### Problem: "Seed data already exists" ale brak użytkowników w UI
**Rozwiązanie:**
```bash
# Sprawdź czy użytkownicy rzeczywiście istnieją w bazie
docker-compose exec postgres psql -U timetracker_user -d timetracker -c "SELECT COUNT(*) FROM \"Users\";"

# Jeśli pokazuje 0, aplikacja nie widzi danych - problem z connection string
# Jeśli pokazuje 3+, dane są - problem z UI/autoryzacją
```

#### Problem: "User created successfully" ale nie widoczny w bazie
**Rozwiązanie:**
```bash
# Możliwe że transakcja nie została zatwierdzona
# Sprawdź logi na błędy dotyczące SaveChanges
docker-compose logs app | grep -i "savechanges\|transaction\|commit"
```

#### Problem: Automatyczne logowanie po rejestracji
**Status:** Naprawione w ostatnim commicie
- Rejestracja przekierowuje teraz do strony logowania
- Wyświetla komunikat sukcesu: "Konto zostało utworzone. Możesz się teraz zalogować."

### 10. Przydatne komendy

```bash
# Pełen restart z czyszczeniem
docker-compose down -v
rm -rf postgres-data/
docker-compose up -d --build

# Sprawdź wszystko
docker-compose ps
docker-compose logs app | tail -50
docker-compose exec postgres psql -U timetracker_user -d timetracker -c "SELECT COUNT(*) FROM \"Users\";"

# Backup bazy danych przed zmianami
cp -r postgres-data/ postgres-data-backup-$(date +%Y%m%d-%H%M%S)/
```

## Dalsze wsparcie

Jeśli problem nadal występuje:

1. **Zbierz informacje diagnostyczne:**
```bash
echo "=== Status kontenerów ===" > debug.log
docker-compose ps >> debug.log
echo "\n=== Logi aplikacji ===" >> debug.log
docker-compose logs app | tail -100 >> debug.log
echo "\n=== Logi PostgreSQL ===" >> debug.log
docker-compose logs postgres | tail -50 >> debug.log
echo "\n=== Connection string ===" >> debug.log
docker-compose exec app printenv | grep ConnectionStrings >> debug.log
echo "\n=== Ilość użytkowników ===" >> debug.log
docker-compose exec postgres psql -U timetracker_user -d timetracker -c "SELECT COUNT(*) FROM \"Users\";" >> debug.log
```

2. **Udostępnij plik `debug.log`** w issue na GitHub
