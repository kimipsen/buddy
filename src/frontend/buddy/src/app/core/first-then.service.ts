import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

// docs/backend/analysis/first-then-board.md. The state changes are idempotent POST actions, so they
// go out with plain post, not postIdempotent. Every write names the revision the caller was shown;
// a stale one is a 409 with FIRST_THEN_BOARD_CHANGED.
export type FirstThenState = Schemas['FirstThenState'];
export type FirstThenCard = Schemas['FirstThenCardResponse'];
export type FirstThenBoard = Schemas['FirstThenBoardResponse'];
export type GuardianFirstThen = Schemas['GuardianFirstThenResponse'];

export const FIRST_THEN_BOARD_CHANGED = 'first_then_board_changed';

export interface NewFirstThenBoard {
  revision: number;
  first: FirstThenCard;
  then: FirstThenCard;
  thenMinutes: number | null;
}

export type FirstThenAction = 'first/complete' | 'first/reopen' | 'end';

@Injectable({ providedIn: 'root' })
export class FirstThenService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private get base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/first-then`;
  }

  // --- The child's own routes ---

  getMyBoard(): Promise<FirstThenBoard> {
    return firstValueFrom(this.http.get<FirstThenBoard>(`${this.base}/me`));
  }

  actOnMyBoard(action: FirstThenAction, revision: number): Promise<FirstThenBoard> {
    return firstValueFrom(
      this.http.post<FirstThenBoard>(`${this.base}/me/${action}`, { revision }),
    );
  }

  // --- A guardian's routes ---

  getBoard(childId: string): Promise<GuardianFirstThen> {
    return firstValueFrom(this.http.get<GuardianFirstThen>(`${this.base}/children/${childId}`));
  }

  setBoard(childId: string, board: NewFirstThenBoard): Promise<FirstThenBoard> {
    return firstValueFrom(this.http.put<FirstThenBoard>(`${this.base}/children/${childId}`, board));
  }

  act(childId: string, action: FirstThenAction, revision: number): Promise<FirstThenBoard> {
    return firstValueFrom(
      this.http.post<FirstThenBoard>(`${this.base}/children/${childId}/${action}`, { revision }),
    );
  }
}
