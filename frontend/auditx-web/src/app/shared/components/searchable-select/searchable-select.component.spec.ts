import { TestBed } from '@angular/core/testing';

import {
  SearchableSelectComponent,
  SelectOption,
} from './searchable-select.component';
import { provideTestEnv } from '../../../../testing/test-providers';

const OPTIONS: SelectOption[] = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Bravo' },
  { value: 'c', label: 'Charlie' },
];

describe('SearchableSelectComponent', () => {
  function create(): SearchableSelectComponent {
    TestBed.configureTestingModule({
      imports: [SearchableSelectComponent],
      providers: [provideTestEnv()],
    });
    const fixture = TestBed.createComponent(SearchableSelectComponent);
    fixture.componentRef.setInput('options', OPTIONS);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('filters options by the query (case-insensitive)', () => {
    const c = create();
    c.query.set('ar'); // matches "Charlie"
    expect(c.filtered().map((o) => o.value)).toEqual(['c']);
  });

  it('keeps the selected option visible even when filtered out', () => {
    const c = create();
    c.writeValue('a'); // Alpha selected
    c.query.set('bravo'); // would exclude Alpha
    const values = c.filtered().map((o) => o.value);
    expect(values).toContain('a');
    expect(values).toContain('b');
  });

  it('round-trips through the ControlValueAccessor', () => {
    const c = create();
    let emitted: string | null | undefined;
    c.registerOnChange((v) => (emitted = v));
    c.writeValue('b');
    expect(c.value()).toBe('b');
    c.select('c');
    expect(c.value()).toBe('c');
    expect(emitted).toBe('c');
  });

  it('clears the query when the panel closes', () => {
    const c = create();
    c.query.set('foo');
    c.onOpened(false);
    expect(c.query()).toBe('');
  });
});
