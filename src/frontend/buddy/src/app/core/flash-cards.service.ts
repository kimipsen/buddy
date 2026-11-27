import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { postIdempotent } from './http-idempotency';
import { RuntimeConfigService } from './runtime-config.service';

export type FlashCardSet = Schemas['FlashCardSetResponse'];
export type FlashCardSetKind = Schemas['FlashCardSetKind'];
export type FlashCardRequest = Schemas['FlashCardRequest'];
export type CreateFlashCardSetRequest = Schemas['CreateFlashCardSetRequest'];
export type UpdateFlashCardSetRequest = Schemas['UpdateFlashCardSetRequest'];
export type FlashCardTierRequest = Schemas['FlashCardTierRequest'];
export type EnabledFlashCardSet = Schemas['EnabledFlashCardSetResponse'];
export type ChildFlashCards = Schemas['ChildFlashCardsResponse'];
export type MyFlashCardSet = Schemas['MyFlashCardSetResponse'];
export type FlashCardDeck = Schemas['FlashCardDeckResponse'];
export type FinishFlashCardGameRequest = Schemas['FinishFlashCardGameRequest'];
export type FinishFlashCardGameResponse = Schemas['FinishFlashCardGameResponse'];

// The 422 codes finishing a game answers with (FinishFlashCardGameOutcome on the backend), and the
// two 409 codes finishing can meet.
export const GAME_ID_REUSED = 'game_id_reused';
export const GAME_NO_LONGER_AVAILABLE = 'game_no_longer_available';
export const CONCURRENCY_CONFLICT = 'concurrency_conflict';
export const IDEMPOTENCY_KEY_IN_PROGRESS = 'idempotency_key_in_progress';

// A 409 whose error envelope carries this code.
export function isConflict(error: unknown, code: string): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 409 &&
    (error.error as { code?: string } | null)?.code === code
  );
}

@Injectable({ providedIn: 'root' })
export class FlashCardsService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private get base(): string {
    return `${this.runtimeConfig.apiBaseUrl}/flash-cards`;
  }

  // --- The guardian's library ---

  listSets(): Promise<FlashCardSet[]> {
    return firstValueFrom(this.http.get<FlashCardSet[]>(`${this.base}/sets`));
  }

  createSet(request: CreateFlashCardSetRequest): Promise<FlashCardSet> {
    return firstValueFrom(this.http.post<FlashCardSet>(`${this.base}/sets`, request));
  }

  updateSet(setId: string, request: UpdateFlashCardSetRequest): Promise<FlashCardSet> {
    return firstValueFrom(this.http.put<FlashCardSet>(`${this.base}/sets/${setId}`, request));
  }

  deleteSet(setId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/sets/${setId}`));
  }

  addCard(setId: string, request: FlashCardRequest): Promise<FlashCardSet> {
    return firstValueFrom(
      this.http.post<FlashCardSet>(`${this.base}/sets/${setId}/cards`, request),
    );
  }

  updateCard(setId: string, cardId: string, request: FlashCardRequest): Promise<FlashCardSet> {
    return firstValueFrom(
      this.http.put<FlashCardSet>(`${this.base}/sets/${setId}/cards/${cardId}`, request),
    );
  }

  removeCard(setId: string, cardId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/sets/${setId}/cards/${cardId}`));
  }

  // --- A guardian's per-child setup ---

  getChildFlashCards(childId: string): Promise<ChildFlashCards> {
    return firstValueFrom(this.http.get<ChildFlashCards>(`${this.base}/children/${childId}`));
  }

  enableSet(
    childId: string,
    setId: string,
    tiers: FlashCardTierRequest[],
  ): Promise<EnabledFlashCardSet> {
    return firstValueFrom(
      this.http.put<EnabledFlashCardSet>(`${this.base}/children/${childId}/sets/${setId}`, {
        tiers,
      }),
    );
  }

  disableSet(childId: string, setId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/children/${childId}/sets/${setId}`));
  }

  // --- The child's own routes ---

  listMySets(): Promise<MyFlashCardSet[]> {
    return firstValueFrom(this.http.get<MyFlashCardSet[]>(`${this.base}/me/sets`));
  }

  getDeck(setId: string, tierId: string): Promise<FlashCardDeck> {
    return firstValueFrom(
      this.http.get<FlashCardDeck>(`${this.base}/me/sets/${setId}/deck`, { params: { tierId } }),
    );
  }

  // The request names a GameId the caller made when the game started, so sending it again is a
  // replay, not a second game. postIdempotent retries a dropped connection with one Idempotency-Key.
  // A 409 concurrency_conflict (a concurrent finish or a guardian's config change landed between the
  // server's two reads) is retried once as a new call: the same GameId, a new Idempotency-Key, so the
  // server takes its replay or recovery branch. idempotency_key_in_progress is not retried: the first
  // attempt is still running, and a second one would only meet the same wall.
  async finishGame(request: FinishFlashCardGameRequest): Promise<FinishFlashCardGameResponse> {
    try {
      return await this.postFinish(request);
    } catch (error) {
      if (!isConflict(error, CONCURRENCY_CONFLICT)) {
        throw error;
      }

      return this.postFinish(request);
    }
  }

  private postFinish(request: FinishFlashCardGameRequest): Promise<FinishFlashCardGameResponse> {
    return firstValueFrom(
      postIdempotent<FinishFlashCardGameResponse>(this.http, `${this.base}/me/games`, request),
    );
  }
}
