import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

import { EditScopeDialogComponent, EditScopeDialogData } from './edit-scope-dialog.component';
import { provideTestEnv } from '../../../../testing/test-providers';

describe('EditScopeDialogComponent', () => {
  let fixture: ComponentFixture<EditScopeDialogComponent>;
  let component: EditScopeDialogComponent;
  let ref: jasmine.SpyObj<MatDialogRef<EditScopeDialogComponent>>;

  function setup(overrides: Partial<EditScopeDialogData> = {}): void {
    ref = jasmine.createSpyObj<MatDialogRef<EditScopeDialogComponent>>('MatDialogRef', ['close']);
    TestBed.configureTestingModule({
      imports: [EditScopeDialogComponent],
      providers: [
        provideTestEnv(),
        { provide: MatDialogRef, useValue: ref },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            name: 'Treasury review',
            scopeDescription: 'Deal capture',
            objectives: null,
            startDate: '2027-01-10',
            targetEndDate: '2027-02-10',
            scopeLocked: false,
            ...overrides,
          } satisfies EditScopeDialogData,
        },
      ],
    });
    fixture = TestBed.createComponent(EditScopeDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('returns trimmed text, empty fields as null, and dates as yyyy-MM-dd', () => {
    setup();
    component.form.patchValue({ name: '  Treasury review  ', scopeDescription: '   ', objectives: '  Confirm deals daily.  ' });
    component.submit();
    expect(ref.close).toHaveBeenCalledWith({
      name: 'Treasury review',
      scopeDescription: null,
      objectives: 'Confirm deals daily.',
      startDate: '2027-01-10',
      targetEndDate: '2027-02-10',
    });
  });

  it('refuses an end date before the start date', () => {
    setup();
    component.form.patchValue({ targetEndDate: new Date(2027, 0, 1) });
    expect(component.datesInverted()).toBeTrue();
    component.submit();
    expect(ref.close).not.toHaveBeenCalled();
  });

  it('keeps an approved scope and objectives read-only but still sends them', () => {
    setup({ scopeLocked: true, objectives: 'Approved aim' });
    expect(component.form.controls.scopeDescription.disabled).toBeTrue();
    expect(component.form.controls.objectives.disabled).toBeTrue();
    component.submit();
    expect(ref.close).toHaveBeenCalledWith(jasmine.objectContaining({ scopeDescription: 'Deal capture', objectives: 'Approved aim' }));
  });
});
