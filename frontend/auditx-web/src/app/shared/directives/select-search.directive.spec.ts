import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { By } from '@angular/platform-browser';

import { SelectSearchDirective } from './select-search.directive';

@Component({
  imports: [MatSelectModule, SelectSearchDirective],
  template: `
    <mat-select [(value)]="value">
      @for (o of options; track o) {
        <mat-option [value]="o">{{ o }}</mat-option>
      }
    </mat-select>
    <mat-select axNoSearch class="plain">
      <mat-option value="a">A</mat-option>
    </mat-select>
  `,
})
class HostComponent {
  readonly options = ['Audit Manager', 'Auditor', 'Audit Committee Chair', 'Risk Owner'];
  value = signal<string | null>('Risk Owner');
}

describe('SelectSearchDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let select: MatSelect;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    select = fixture.debugElement.queryAll(By.directive(MatSelect))[0].componentInstance as MatSelect;
  });

  const nextTick = () => new Promise<void>((resolve) => setTimeout(resolve));

  /** Opens a select and waits for its panel and the search header (added on the next tick). */
  async function open(target: MatSelect = select): Promise<HTMLElement> {
    target.open();
    fixture.detectChanges();
    await fixture.whenStable();
    await nextTick();
    await nextTick();
    fixture.detectChanges();
    return target.panel.nativeElement as HTMLElement;
  }

  function visibleOptions(): string[] {
    return select.options.filter((o) => o._getHostElement().style.display !== 'none').map((o) => o.viewValue);
  }

  it('adds a search box to the top of the list when it opens', async () => {
    const panel = await open();

    const input = panel.querySelector<HTMLInputElement>('.ax-select-head .ax-select-search__input');
    expect(input).not.toBeNull();
    expect(panel.firstElementChild?.classList.contains('ax-select-head')).toBeTrue();
  });

  it('narrows the options to those matching the search, keeping the selected one', async () => {
    const panel = await open();
    const input = panel.querySelector<HTMLInputElement>('.ax-select-search__input')!;

    input.value = 'audit';
    input.dispatchEvent(new Event('input'));

    expect(visibleOptions()).toEqual(['Audit Manager', 'Auditor', 'Audit Committee Chair', 'Risk Owner']);

    input.value = 'committee';
    input.dispatchEvent(new Event('input'));

    expect(visibleOptions()).toEqual(['Audit Committee Chair', 'Risk Owner']);
    expect(panel.querySelector<HTMLElement>('.ax-select-search__empty')!.hidden).toBeTrue();
  });

  it('says when nothing matches', async () => {
    select.value = null;
    const panel = await open();
    const input = panel.querySelector<HTMLInputElement>('.ax-select-search__input')!;

    input.value = 'zzz';
    input.dispatchEvent(new Event('input'));

    expect(visibleOptions()).toEqual([]);
    expect(panel.querySelector<HTMLElement>('.ax-select-search__empty')!.hidden).toBeFalse();
  });

  it('shows every option again the next time the list opens', async () => {
    let panel = await open();
    const input = panel.querySelector<HTMLInputElement>('.ax-select-search__input')!;
    input.value = 'committee';
    input.dispatchEvent(new Event('input'));

    select.close();
    fixture.detectChanges();
    await fixture.whenStable();
    await nextTick();
    panel = await open();

    expect(visibleOptions().length).toBe(4);
    expect(panel.querySelector<HTMLInputElement>('.ax-select-search__input')!.value).toBe('');
  });

  it('keeps typed characters out of the list type-ahead', async () => {
    const panel = await open();
    const input = panel.querySelector<HTMLInputElement>('.ax-select-search__input')!;
    const event = new KeyboardEvent('keydown', { key: 'a', bubbles: true });
    const stop = spyOn(event, 'stopPropagation').and.callThrough();

    input.dispatchEvent(event);

    expect(stop).toHaveBeenCalled();
  });

  it('leaves a select marked axNoSearch alone', async () => {
    const plain = fixture.debugElement.queryAll(By.directive(MatSelect))[1].componentInstance as MatSelect;
    const panel = await open(plain);

    expect(panel.querySelector('.ax-select-head')).toBeNull();
  });
});
