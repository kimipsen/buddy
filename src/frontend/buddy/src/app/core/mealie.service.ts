import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import type { Schemas } from './api/schemas';
import { postIdempotent } from './http-idempotency';
import type { MealSlot } from './mealplans.service';
import { RuntimeConfigService } from './runtime-config.service';

// The family's Mealie connection (docs/backend/analysis/mealie-integration.md): { kind: 0 } when
// not connected, { kind: 1, ... } with the address, household and the token's last 4 characters.
export type MealieConnectionView = Schemas['MealieConnectionView'];
export type MealieConnection = Schemas['MealieConnectionViewConnected'];
export type TestMealieConnectionResult = Schemas['TestMealieConnectionResult'];
export type MealieRecipePage = Schemas['MealieRecipePage'];
export type MealieRecipeSummary = Schemas['MealieRecipeSummary'];
export type MealieRecipeDetails = Schemas['MealieRecipeDetails'];
export type MealieRecipeRef = Schemas['MealieRecipeRef'];
export type MealieShoppingList = Schemas['MealieShoppingList'];
export type AddedToShoppingList = Schemas['AddedToShoppingList'];
export type MealieImportPreview = Schemas['MealieImportPreview'];

export const MEALIE_CONNECTED = 1;

// The error codes the Mealie routes answer with besides validation: 409 when the family isn't
// connected, 502 when the family's Mealie fails.
export const MEALIE_ERROR_KEYS: Record<string, string> = {
  mealie_not_connected: 'mealie.errors.notConnected',
  mealie_unreachable: 'mealie.errors.unreachable',
  mealie_token_rejected: 'mealie.errors.tokenRejected',
  mealie_error: 'mealie.errors.error',
};

// The i18n key for a failed Mealie call: what the 409/502 code says, else the caller's fallback.
export function mealieErrorKey(error: unknown, fallback: string): string {
  const code =
    error instanceof HttpErrorResponse
      ? (error.error as { code?: string } | null)?.code
      : undefined;

  return (code === undefined ? undefined : MEALIE_ERROR_KEYS[code]) ?? fallback;
}

export function isMealieConnected(view: MealieConnectionView | null): view is MealieConnection {
  return view?.kind === MEALIE_CONNECTED;
}

// Every Mealie call goes through Buddy's API, keyed by the family's first child like the AI
// assistant: the browser can't call Mealie (no CORS) and never sees the token.
@Injectable({ providedIn: 'root' })
export class MealieService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private base(childId: string): string {
    return `${this.runtimeConfig.apiBaseUrl}/mealplans/children/${childId}`;
  }

  getConnection(childId: string): Promise<MealieConnectionView> {
    return firstValueFrom(this.http.get<MealieConnectionView>(`${this.base(childId)}/mealie`));
  }

  connect(childId: string, baseUrl: string, apiToken: string): Promise<MealieConnectionView> {
    return firstValueFrom(
      this.http.put<MealieConnectionView>(`${this.base(childId)}/mealie`, { baseUrl, apiToken }),
    );
  }

  disconnect(childId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base(childId)}/mealie`));
  }

  testConnection(childId: string): Promise<TestMealieConnectionResult> {
    return firstValueFrom(
      this.http.post<TestMealieConnectionResult>(
        `${this.base(childId)}/mealie/test-connection`,
        {},
      ),
    );
  }

  searchRecipes(childId: string, search: string, page: number): Promise<MealieRecipePage> {
    return firstValueFrom(
      this.http.get<MealieRecipePage>(`${this.base(childId)}/mealie/recipes`, {
        params: { search, page },
      }),
    );
  }

  getRecipe(childId: string, recipeId: string): Promise<MealieRecipeDetails> {
    return firstValueFrom(
      this.http.get<MealieRecipeDetails>(`${this.base(childId)}/mealie/recipes/${recipeId}`),
    );
  }

  // A blob, not an <img src>: the route needs the bearer token the interceptor adds.
  getRecipeImage(childId: string, recipeId: string): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.base(childId)}/mealie/recipes/${recipeId}/image`, {
        responseType: 'blob',
      }),
    );
  }

  // Mealie's plan for the range as the import review; a POST like the text preview, it writes nothing.
  previewImport(childId: string, from: string, to: string): Promise<MealieImportPreview> {
    return firstValueFrom(
      this.http.post<MealieImportPreview>(`${this.base(childId)}/imports/mealie/preview`, {
        from,
        to,
      }),
    );
  }

  listShoppingLists(childId: string): Promise<MealieShoppingList[]> {
    return firstValueFrom(
      this.http.get<MealieShoppingList[]>(`${this.base(childId)}/mealie/shopping-lists`),
    );
  }

  // Idempotent: Mealie adds quantities on every call, so a retry must not reach it twice.
  addPlanToShoppingList(
    childId: string,
    listId: string,
    from: string,
    to: string,
    slots: MealSlot[],
  ): Promise<AddedToShoppingList> {
    return firstValueFrom(
      postIdempotent<AddedToShoppingList>(
        this.http,
        `${this.base(childId)}/mealie/shopping-lists/${listId}/recipes`,
        { from, to, slots },
      ),
    );
  }
}
