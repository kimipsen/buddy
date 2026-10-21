import {
  Component,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TranslatePipe } from '../../../../../core/i18n/translate.pipe';
import {
  MealieRecipeSummary,
  MealieService,
  mealieErrorKey,
} from '../../../../../core/mealie.service';

// Searches the family's Mealie recipes and hands back the one chosen -- for linking a meal, or for
// creating a meal from it (the parent decides). Inline below the meal list rather than a dialog.
@Component({
  selector: 'app-mealie-recipe-picker',
  imports: [FormsModule, TranslatePipe],
  templateUrl: './mealie-recipe-picker.html',
})
export class MealieRecipePicker implements OnInit {
  private readonly mealie = inject(MealieService);
  private readonly injector = inject(Injector);

  readonly childId = input.required<string>();
  // Already translated: "Link “Lasagne” to a Mealie recipe" or "Add a meal from a Mealie recipe".
  readonly heading = input.required<string>();
  readonly busy = input(false);

  readonly picked = output<MealieRecipeSummary>();
  readonly closed = output<void>();

  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('searchInput');

  protected readonly search = signal('');
  protected readonly recipes = signal<MealieRecipeSummary[]>([]);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  // Stryker disable next-line BooleanLiteral: ngOnInit starts the first load, setting it, before the first render
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    void this.load(1);
    afterNextRender(() => this.searchInput()?.nativeElement.focus(), { injector: this.injector });
  }

  protected runSearch(): void {
    void this.load(1);
  }

  protected showMore(): void {
    void this.load(this.page() + 1);
  }

  // Only the latest search or "Show more" may write the results, so an answer that arrives late
  // (an old query's page 2 after a new search) is dropped.
  private request = 0;

  private async load(page: number): Promise<void> {
    // Stryker disable next-line UpdateOperator: counting down gives every load a unique number too
    const request = ++this.request;
    this.loading.set(true);
    this.error.set(null);

    try {
      const result = await this.mealie.searchRecipes(this.childId(), this.search().trim(), page);

      if (request !== this.request) {
        return;
      }

      this.recipes.update((current) => (page === 1 ? result.items : [...current, ...result.items]));
      this.page.set(result.page);
      this.totalPages.set(result.totalPages);
    } catch (error) {
      if (request === this.request) {
        this.error.set(mealieErrorKey(error, 'mealie.errors.error'));
      }
    } finally {
      if (request === this.request) {
        this.loading.set(false);
      }
    }
  }
}
