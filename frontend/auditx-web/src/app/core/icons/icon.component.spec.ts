import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { provideTestEnv } from '../../../testing/test-providers';
import { IconComponent } from './icon.component';
import { FALLBACK_ICON, MATERIAL_TO_LUCIDE } from './icon-registry';

@Component({
  imports: [IconComponent],
  template: `<app-icon [name]="name" />`,
})
class HostComponent {
  name = 'report_problem';
}

describe('IconComponent', () => {
  let fixture: ComponentFixture<HostComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideTestEnv()],
    });
    fixture = TestBed.createComponent(HostComponent);
  });

  it('renders an SVG for a mapped Material name', () => {
    fixture.detectChanges();
    const svg = fixture.nativeElement.querySelector('app-icon svg');
    expect(svg).toBeTruthy();
  });

  it('renders (via the fallback) for an unmapped name without erroring', () => {
    fixture.componentInstance.name = 'definitely_not_an_icon';
    fixture.detectChanges();
    const svg = fixture.nativeElement.querySelector('app-icon svg');
    expect(svg).toBeTruthy();
  });

  it('maps the icons the templates rely on to real Lucide data', () => {
    // A representative spread across the app; every value must be defined icon data.
    for (const key of ['report_problem', 'check_circle', 'more_vert', 'history', 'dashboard', 'visibility_off']) {
      expect(MATERIAL_TO_LUCIDE[key]).withContext(key).toBeTruthy();
    }
    expect(FALLBACK_ICON).toBeTruthy();
  });
});
