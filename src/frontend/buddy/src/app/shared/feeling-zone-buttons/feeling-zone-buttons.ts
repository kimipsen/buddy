import { Component, inject, input, output } from '@angular/core';

import { ZONES, ZoneInfo } from '../../core/feelings';
import type { FeelingZone, FeelingsDisplayMode } from '../../core/feelings.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

/**
 * The four feelings buttons: a face, the zone's colour and its word, as real buttons with
 * `aria-pressed` (docs/backend/analysis/feelings-check-in.md, Frontend). `size="child"` is the
 * child's home card (at least 64px tall, 2x2 then one row); `size="guardian"` the guardian's form.
 * In `FacesOnly` mode (Question 9) the word is hidden and the accessible name is the face's, with
 * no colour or zone name.
 */
@Component({
  selector: 'app-feeling-zone-buttons',
  imports: [TranslatePipe],
  templateUrl: './feeling-zone-buttons.html',
})
export class FeelingZoneButtons {
  private readonly translation = inject(TranslationService);

  readonly selected = input<FeelingZone | null>(null);
  readonly displayMode = input<FeelingsDisplayMode>('FacesAndWords');
  readonly size = input<'child' | 'guardian'>('child');
  readonly disabled = input(false);
  readonly groupLabel = input('');

  readonly picked = output<FeelingZone>();

  protected readonly zones = ZONES;

  protected accessibleName(zone: ZoneInfo): string {
    return this.displayMode() === 'FacesOnly'
      ? this.translation.translate(zone.faceNameKey)
      : this.translation.translate('feelings.zones.buttonLabel', {
          label: this.translation.translate(zone.labelKey),
          meaning: this.translation.translate(zone.meaningKey),
        });
  }
}
