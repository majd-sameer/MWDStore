import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideCore } from 'core';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';
import { App } from './app';
import { JsonTranslateLoader } from './core/translate-loader';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideCore({}),
        provideTranslateService({
          lang: 'en',
          fallbackLang: 'en',
          loader: { provide: TranslateLoader, useClass: JsonTranslateLoader },
        }),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the brand wordmark in the header', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    // The visible name is translated (async dictionary import); the static
    // aria-label carries the brand and renders synchronously.
    const wordmark = compiled.querySelector('app-header .wordmark');
    expect(wordmark?.getAttribute('aria-label')).toBe('MadeWithDetermination');
    expect(wordmark?.querySelector('img')?.getAttribute('src')).toBe('logo-gold.png');
  });
});
