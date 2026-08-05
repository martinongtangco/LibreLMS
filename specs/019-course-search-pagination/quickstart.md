# Quickstart: Validate Course Browse Search, Filter, and Pagination

**Feature**: specs/019-course-search-pagination/spec.md  
**Date**: 2025-07-31

## Prerequisites

1. Docker Compose services running:
   ```bash
   docker compose up -d mssql valkey
   ```

2. Database initialized (handled automatically on app startup via `EnsureDeleted()` + `Migrate()`):
   ```bash
   cd src/Host
   dotnet run
   ```

3. Application running at `http://localhost:5000` (default Kestrel port)

## Validation Scenarios

### 1. Search by Course Title

**Steps**:
1. Navigate to `http://localhost:5000/Courses`
2. Type "python" (or any course title fragment) in the search box
3. Wait ~300ms for debounce to trigger

**Expected**:
- Course list updates without page reload
- Only courses with "python" in the title are visible
- Other courses are hidden
- If no courses match, an empty state message appears

### 2. Filter by Category

**Steps**:
1. Navigate to `http://localhost:5000/Courses`
2. Select a category from the dropdown (e.g., "Programming")

**Expected**:
- Course list updates without page reload
- Only courses in the selected category are visible
- "All Categories" restores the full list

### 3. Combined Search + Category

**Steps**:
1. Enter a search term in the search box
2. Select a category from the dropdown
3. Wait for both results to settle

**Expected**:
- Only courses matching BOTH the search term AND the category are shown
- Results are a subset of either filter alone

### 4. Pagination

**Steps**:
1. Ensure the catalog has more than 12 courses (seeded data should provide this)
2. Navigate to `http://localhost:5000/Courses`
3. Verify only 12 courses are shown on the first page
4. Click "Next" to go to page 2

**Expected**:
- Page 1 shows courses 1-12 (alphabetically by title)
- Page 2 shows courses 13-24
- "Previous" button is disabled on page 1
- "Next" button is disabled on the last page
- Page navigation preserves any active search/filter

### 5. Filter Change Resets to Page 1

**Steps**:
1. Navigate to page 2 or beyond
2. Change the search term or category

**Expected**:
- View returns to page 1 of the new filtered results
- Old page number is not retained

### 6. Clear Filters

**Steps**:
1. Apply any search term and/or category filter
2. Click the "Clear" button

**Expected**:
- Both search and category are reset
- Full course list is shown from page 1

### 7. T-SQL Execution Verification

**Steps**:
1. Connect to the MSSQL database:
   ```bash
   docker exec -it $(docker compose ps -q mssql) /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C
   ```
2. Verify Full-Text Catalog exists:
   ```sql
   SELECT name, is_default FROM sys.fulltext_catalogs;
   ```
3. Verify Full-Text Index on Courses:
   ```sql
   SELECT object_name(object_id) AS table_name, object_name(i.index_id) AS index_name
   FROM sys.fulltext_indexes i;
   ```
4. Verify stored procedure exists:
   ```sql
   SELECT name FROM sys.procedures WHERE name = 'BrowseCourses';
   ```

**Expected**:
- Full-Text Catalog `LearningLmsFtCatalog` exists and is the default
- Full-Text Index is active on `Courses.Title`
- Stored procedure `BrowseCourses` exists

## Rollback

If validation fails, the database can be fully reset by restarting the Host application (it calls `EnsureDeleted()` + `Migrate()` on startup).
