import { provideRouter } from '@angular/router';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { TranslationService } from './core/i18n/translation.service';
import { ThemeService } from './core/theme.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the router outlet', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('keeps <html lang> in sync with the selected language', async () => {
    const fixture = TestBed.createComponent(App);
    const translation = TestBed.inject(TranslationService);

    await translation.setLanguage('da');
    fixture.detectChanges();
    expect(document.documentElement.lang).toBe('da');

    await translation.setLanguage('en');
    fixture.detectChanges();
    expect(document.documentElement.lang).toBe('en');
  });

  it('toggles the dark class on <html> with the theme', () => {
    const fixture = TestBed.createComponent(App);
    const theme = TestBed.inject(ThemeService);

    theme.setMode('dark');
    fixture.detectChanges();
    expect(document.documentElement.classList.contains('dark')).toBe(true);

    theme.setMode('light');
    fixture.detectChanges();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });
});
