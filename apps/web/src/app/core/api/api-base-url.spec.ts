import { TestBed } from '@angular/core/testing';
import { API_BASE_URL, provideApiBaseUrl } from './api-base-url';

describe('API_BASE_URL', () => {
  it('uses the document origin in the browser', () => {
    TestBed.configureTestingModule({ providers: [provideApiBaseUrl()] });

    expect(TestBed.inject(API_BASE_URL)).toBe(document.location.origin);
  });
});
