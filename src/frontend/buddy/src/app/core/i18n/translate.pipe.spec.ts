import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { TranslatePipe } from './translate.pipe';
import { TranslationService } from './translation.service';

describe('TranslatePipe', () => {
  function setup(translate: (key: string, params?: Record<string, string | number>) => string) {
    const translationServiceStub: Partial<TranslationService> = {
      translate: vi.fn(translate)
    };

    TestBed.configureTestingModule({
      providers: [TranslatePipe, { provide: TranslationService, useValue: translationServiceStub }]
    });

    const pipe = TestBed.inject(TranslatePipe);

    return { pipe, translationServiceStub };
  }

  it('is marked impure so it re-runs on every change detection cycle even when the key is unchanged', () => {
    // Impurity is what makes a language switch (which doesn't change the key argument) show up
    // without callers having to manually trigger re-evaluation.
    const metadata = (TranslatePipe as unknown as { ɵpipe: { pure: boolean } }).ɵpipe;

    expect(metadata.pure).toBe(false);
  });

  it('delegates to TranslationService.translate with the given key', () => {
    const { pipe, translationServiceStub } = setup(() => 'translated value');

    const result = pipe.transform('common.greeting');

    expect(translationServiceStub.translate).toHaveBeenCalledWith('common.greeting', undefined);
    expect(result).toBe('translated value');
  });

  it('forwards params through to TranslationService.translate untouched', () => {
    const { pipe, translationServiceStub } = setup(() => 'translated value');
    const params = { name: 'Sam', count: 3 };

    pipe.transform('common.farewell', params);

    expect(translationServiceStub.translate).toHaveBeenCalledWith('common.farewell', params);
  });

  it('returns exactly what TranslationService.translate returns, including a raw-key fallback', () => {
    const { pipe } = setup((key) => key);

    expect(pipe.transform('missing.key')).toBe('missing.key');
  });

  it('re-resolves the key on every call, reflecting a language change without re-invoking with a new key', () => {
    let language: 'en' | 'da' = 'en';
    const { pipe } = setup(() => (language === 'en' ? 'Hello' : 'Hej'));

    expect(pipe.transform('greeting')).toBe('Hello');

    language = 'da';

    expect(pipe.transform('greeting')).toBe('Hej');
  });
});
