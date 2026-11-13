import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ExpandableRecord } from './expandable-record';
import { PortalListFilters } from './portal-list-filters';
import { PortalTabs } from './portal-tabs';

describe('shared portal controls', () => {
  it('emits the selected tab and exposes its selected state accessibly', async () => {
    await TestBed.configureTestingModule({ imports: [PortalTabs] }).compileComponents();

    const fixture = TestBed.createComponent(PortalTabs);
    fixture.componentInstance.tabs = [
      { id: 'overview', label: 'Overview', icon: 'overview' },
      { id: 'create', label: 'Create donation', icon: 'create' },
    ];
    fixture.componentInstance.activeTab = 'overview';
    fixture.componentInstance.label = 'Donor workspace';
    const change = vi.fn();
    fixture.componentInstance.tabChange.subscribe(change);
    fixture.detectChanges();

    const tabs = fixture.nativeElement.querySelectorAll('[role="tab"]') as NodeListOf<HTMLButtonElement>;
    expect(fixture.nativeElement.querySelectorAll('.tab-icon')).toHaveLength(2);
    expect(tabs[0].getAttribute('aria-selected')).toBe('true');
    expect(tabs[1].getAttribute('aria-selected')).toBe('false');
    tabs[1].click();
    expect(change).toHaveBeenCalledWith('create');
  });

  it('emits list search, category, and status filter changes', async () => {
    await TestBed.configureTestingModule({ imports: [PortalListFilters] }).compileComponents();

    const fixture = TestBed.createComponent(PortalListFilters);
    fixture.componentInstance.types = [
      { value: 'All', label: 'All' },
      { value: 'Groceries', label: 'Groceries' },
    ];
    fixture.componentInstance.statuses = [
      { value: 'All', label: 'All' },
      { value: 'Available', label: 'Available' },
    ];
    const queryChange = vi.fn();
    const typeChange = vi.fn();
    const statusChange = vi.fn();
    fixture.componentInstance.queryChange.subscribe(queryChange);
    fixture.componentInstance.typeChange.subscribe(typeChange);
    fixture.componentInstance.statusChange.subscribe(statusChange);
    fixture.detectChanges();

    const search = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    search.value = 'rice';
    search.dispatchEvent(new Event('input'));
    const selects = fixture.nativeElement.querySelectorAll('select') as NodeListOf<HTMLSelectElement>;
    selects[0].value = 'Groceries';
    selects[0].dispatchEvent(new Event('change'));
    selects[1].value = 'Available';
    selects[1].dispatchEvent(new Event('change'));

    expect(queryChange).toHaveBeenCalledWith('rice');
    expect(typeChange).toHaveBeenCalledWith('Groceries');
    expect(statusChange).toHaveBeenCalledWith('Available');
  });

  it('shows record details only after the user expands a record', async () => {
    await TestBed.configureTestingModule({ imports: [ExpandableRecord] }).compileComponents();

    const fixture = TestBed.createComponent(ExpandableRecord);
    fixture.componentInstance.title = 'Community meals';
    fixture.componentInstance.category = 'Prepared food';
    fixture.componentInstance.status = 'Available';
    fixture.componentInstance.statusLabel = 'Available';
    fixture.detectChanges();

    const details = fixture.nativeElement.querySelector('details') as HTMLDetailsElement;
    expect(details.open).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Community meals');
    details.open = true;
    expect(details.open).toBe(true);
  });
});
