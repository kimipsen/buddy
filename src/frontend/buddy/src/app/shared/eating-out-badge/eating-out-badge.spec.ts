import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { MealEatingOut } from '../../core/mealplans.service';
import { EatingOutBadge } from './eating-out-badge';

describe('EatingOutBadge', () => {
  async function render(kind: MealEatingOut): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [EatingOutBadge] }).compileComponents();
    const fixture = TestBed.createComponent(EatingOutBadge);
    fixture.componentRef.setInput('kind', kind);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders nothing for a meal eaten at home', async () => {
    expect((await render('None')).textContent?.trim()).toBe('');
  });

  it('names a restaurant', async () => {
    expect((await render('Restaurant')).textContent?.trim()).toBe('Eating out · Restaurant');
  });

  it('names friends and family', async () => {
    expect((await render('FriendsAndFamily')).textContent?.trim()).toBe(
      'Eating out · Friends & family',
    );
  });
});
