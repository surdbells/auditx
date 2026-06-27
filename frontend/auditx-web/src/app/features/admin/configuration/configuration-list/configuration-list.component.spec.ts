import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { ConfigurationListComponent } from './configuration-list.component';
import { provideTestEnv } from '../../../../../testing/test-providers';

describe('ConfigurationListComponent', () => {
  let fixture: ComponentFixture<ConfigurationListComponent>;
  let component: ConfigurationListComponent;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [ConfigurationListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(ConfigurationListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('lists the editable configuration domains', () => {
    expect(component.domains.length).toBeGreaterThan(0);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Exception defaults & SLAs');
    expect(text).toContain('exception_defaults');
  });
});
