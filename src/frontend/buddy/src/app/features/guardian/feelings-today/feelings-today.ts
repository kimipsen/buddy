import { Component, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';

import { todayIsoDate } from '../../../core/date-utils';
import { zoneInfo } from '../../../core/feelings';
import { FeelingCheckIn, FeelingsService } from '../../../core/feelings.service';
import { GuardiansService } from '../../../core/guardians.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  PER_ITEM_REQUEST_CONCURRENCY,
  mapWithConcurrency,
} from '../../../core/map-with-concurrency';
import { Card } from '../../../shared/card/card';
import { LoadingSpinner } from '../../../shared/loading-spinner/loading-spinner';

interface ChildFeelings {
  childId: string;
  name: string;
  checkIns: FeelingCheckIn[];
}

interface LoadedFeelings {
  hasChildren: boolean;
  children: ChildFeelings[];
}

/**
 * The dashboard's "Feelings today": each child's check-ins today as face chips, linking to the
 * feelings page. Reading it is a logged health-data read, like app-doses-today.
 */
@Component({
  selector: 'app-feelings-today',
  imports: [Card, LoadingSpinner, RouterLink, TranslatePipe],
  templateUrl: './feelings-today.html',
})
export class FeelingsToday {
  private readonly guardians = inject(GuardiansService);
  private readonly feelings = inject(FeelingsService);

  protected readonly zoneInfo = zoneInfo;
  protected readonly today = resource({ loader: () => this.load() });

  private async load(): Promise<LoadedFeelings> {
    const children = await this.guardians.listMyChildren();
    const today = todayIsoDate();

    const perChild = await mapWithConcurrency(
      children,
      PER_ITEM_REQUEST_CONCURRENCY,
      async (child) => ({
        childId: child.id,
        name: child.name.givenName,
        checkIns: (await this.feelings.listCheckIns(child.id, today, today)).checkIns,
      }),
    );

    return { hasChildren: children.length > 0, children: perChild };
  }
}
