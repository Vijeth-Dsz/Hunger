import { Component, EventEmitter, Input, Output } from '@angular/core';

export interface FilterOption {
  value: string;
  label: string;
}

@Component({
  selector: 'app-portal-list-filters',
  template: `
    <div class="list-filters">
      <label class="filter-search">
        <span>{{ searchLabel }}</span>
        <input type="search" [value]="query" [placeholder]="searchPlaceholder" (input)="updateQuery($event)" />
      </label>
      @if (types.length > 0) {
        <label>
          <span>{{ typeLabel }}</span>
          <select [value]="selectedType" (change)="updateType($event)">
            @for (option of types; track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        </label>
      }
      @if (statuses.length > 0) {
        <label>
          <span>{{ statusLabel }}</span>
          <select [value]="selectedStatus" (change)="updateStatus($event)">
            @for (option of statuses; track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        </label>
      }
    </div>
  `,
  styles: [`
    :host {
      display: block;
      margin: 0 0 14px;
    }

    .list-filters {
      display: grid;
      grid-template-columns: minmax(220px, 1.5fr) repeat(2, minmax(160px, 1fr));
      gap: 12px;
      padding: 14px;
      border: 1px solid var(--line);
      border-radius: 10px;
      background: #fff;
    }

    label {
      display: grid;
      min-width: 0;
      gap: 5px;
      color: #647168;
      font-size: 13px;
      font-weight: 600;
    }

    input,
    select {
      width: 100%;
      min-width: 0;
      min-height: 46px;
      padding: 10px 12px;
      border: 1px solid #dfe5df;
      border-radius: 7px;
      background: #fcfdfb;
      color: var(--ink);
      font: inherit;
      font-size: 14px;
    }

    input:focus,
    select:focus {
      border-color: #8ba894;
      outline: 2px solid #edf3ee;
    }

    @media (max-width: 760px) {
      .list-filters {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }

      .filter-search {
        grid-column: 1 / -1;
      }
    }

    @media (max-width: 420px) {
      .list-filters {
        grid-template-columns: 1fr;
        gap: 10px;
      }

      .filter-search {
        grid-column: auto;
      }

      input,
      select {
        min-height: 48px;
      }
    }
  `],
})
export class PortalListFilters {
  @Input() query = '';
  @Input() searchLabel = '';
  @Input() searchPlaceholder = '';
  @Input() typeLabel = '';
  @Input() statusLabel = '';
  @Input() types: FilterOption[] = [];
  @Input() statuses: FilterOption[] = [];
  @Input() selectedType = 'All';
  @Input() selectedStatus = 'All';

  @Output() queryChange = new EventEmitter<string>();
  @Output() typeChange = new EventEmitter<string>();
  @Output() statusChange = new EventEmitter<string>();

  updateQuery(event: Event): void {
    this.queryChange.emit((event.target as HTMLInputElement).value);
  }

  updateType(event: Event): void {
    this.typeChange.emit((event.target as HTMLSelectElement).value);
  }

  updateStatus(event: Event): void {
    this.statusChange.emit((event.target as HTMLSelectElement).value);
  }
}
