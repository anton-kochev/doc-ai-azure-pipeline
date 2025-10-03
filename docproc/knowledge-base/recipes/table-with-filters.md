---
id: table-with-filters
title: Table with Filters Recipe
category: ui
tags: ["table", "filters", "data-display", "angular"]
version: 1.0.0
frameworks: ["angular"]
difficulty: intermediate
estimatedTokens: 850
---

# Table with Filters Recipe

## Problem

Need to display tabular data with client-side or server-side filtering, sorting, and pagination.

## Solution

Use Angular Material Table with reactive filter state management.

## Implementation

### Component

```typescript
@Component({
  selector: 'app-data-table',
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DataTableComponent implements OnInit {
  // Data source
  dataSource = new MatTableDataSource<DataItem>();
  displayedColumns = ['id', 'name', 'status', 'actions'];

  // Filter state
  filterText = signal('');
  statusFilter = signal<string | null>(null);

  private readonly dataService = inject(DataService);

  // Computed filtered data
  filteredData = computed(() => {
    let data = this.dataSource.data;
    const text = this.filterText().toLowerCase();
    const status = this.statusFilter();

    if (text) {
      data = data.filter(item =>
        item.name.toLowerCase().includes(text)
      );
    }

    if (status) {
      data = data.filter(item => item.status === status);
    }

    return data;
  });

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.dataService.getData()
      .pipe(takeUntilDestroyed())
      .subscribe(data => {
        this.dataSource.data = data;
      });
  }

  applyFilter(filterValue: string): void {
    this.filterText.set(filterValue);
  }

  setStatusFilter(status: string | null): void {
    this.statusFilter.set(status);
  }
}
```

### Template

```html
<mat-form-field>
  <mat-label>Search</mat-label>
  <input matInput (keyup)="applyFilter($event.target.value)">
</mat-form-field>

<mat-form-field>
  <mat-label>Status</mat-label>
  <mat-select (selectionChange)="setStatusFilter($event.value)">
    <mat-option [value]="null">All</mat-option>
    <mat-option value="active">Active</mat-option>
    <mat-option value="inactive">Inactive</mat-option>
  </mat-select>
</mat-form-field>

<table mat-table [dataSource]="filteredData()">
  <ng-container matColumnDef="id">
    <th mat-header-cell *matHeaderCellDef>ID</th>
    <td mat-cell *matCellDef="let item">{{item.id}}</td>
  </ng-container>

  <ng-container matColumnDef="name">
    <th mat-header-cell *matHeaderCellDef>Name</th>
    <td mat-cell *matCellDef="let item">{{item.name}}</td>
  </ng-container>

  <ng-container matColumnDef="status">
    <th mat-header-cell *matHeaderCellDef>Status</th>
    <td mat-cell *matCellDef="let item">{{item.status}}</td>
  </ng-container>

  <ng-container matColumnDef="actions">
    <th mat-header-cell *matHeaderCellDef>Actions</th>
    <td mat-cell *matCellDef="let item">
      <button mat-icon-button>
        <mat-icon>edit</mat-icon>
      </button>
    </td>
  </ng-container>

  <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
  <tr mat-row *matRowDef="let row; columns: displayedColumns;"></tr>
</table>
```

## Notes

- For server-side filtering, replace computed signal with HTTP calls
- Add `MatPaginator` and `MatSort` for pagination and sorting
- Use `trackBy` function for better performance with large datasets
