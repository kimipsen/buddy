import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, output, resource, signal } from '@angular/core';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import {
  MealieRecipeDetails,
  MealieService,
  mealieErrorKey,
} from '../../../../../core/mealie.service';

type LoadedRecipe =
  | { found: true; recipe: MealieRecipeDetails }
  // Deleted in Mealie since the meal was linked: the meal keeps its link and the stored name.
  | { found: false };

// A linked meal's recipe, fetched live from the family's Mealie (Buddy stores only the link):
// photo, time, yield, ingredients and steps, with "Open in Mealie", "Change recipe" and "Unlink".
@Component({
  selector: 'app-mealie-recipe-panel',
  imports: [TranslatePipe],
  templateUrl: './mealie-recipe-panel.html',
})
export class MealieRecipePanel {
  private readonly mealie = inject(MealieService);

  readonly childId = input.required<string>();
  readonly recipeId = input.required<string>();
  readonly mealName = input.required<string>();
  readonly busy = input(false);

  readonly changeRecipe = output<void>();
  readonly unlink = output<void>();
  readonly closed = output<void>();

  protected readonly recipe = resource({
    params: () => ({ childId: this.childId(), recipeId: this.recipeId() }),
    loader: ({ params }) => this.load(params.childId, params.recipeId),
  });

  // An object URL for the proxied photo (the image route needs the bearer token, so no plain src).
  protected readonly photoUrl = signal<string | null>(null);

  // Bumped by every recipe load and by destroy, so a photo that arrives for an earlier recipe (or
  // after the panel closed) is dropped instead of shown or leaked.
  private photoRequest = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.photoRequest++;
      this.revokePhoto();
    });
  }

  protected errorKey(error: unknown): string {
    return mealieErrorKey(error, 'mealie.errors.error');
  }

  private async load(childId: string, recipeId: string): Promise<LoadedRecipe> {
    const request = ++this.photoRequest;
    this.revokePhoto();

    try {
      const recipe = await this.mealie.getRecipe(childId, recipeId);

      if (recipe.hasImage && request === this.photoRequest) {
        void this.loadPhoto(childId, recipeId, request);
      }

      return { found: true, recipe };
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return { found: false };
      }

      throw error;
    }
  }

  // Best effort: a recipe without a usable photo shows without one.
  private async loadPhoto(childId: string, recipeId: string, request: number): Promise<void> {
    try {
      const blob = await this.mealie.getRecipeImage(childId, recipeId);

      // The load that started this request already revoked the previous photo.
      if (request === this.photoRequest) {
        this.photoUrl.set(URL.createObjectURL(blob));
      }
    } catch {
      // Best effort: the recipe shows without a photo.
    }
  }

  private revokePhoto(): void {
    const url = this.photoUrl();

    if (url) {
      URL.revokeObjectURL(url);
      this.photoUrl.set(null);
    }
  }
}
