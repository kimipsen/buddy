import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { AiAssistantService, AiProviderSettings, AiSessionView, StartAiSessionRequest } from './ai-assistant.service';
import { RuntimeConfigService } from './runtime-config.service';

describe('AiAssistantService', () => {
  let service: AiAssistantService;
  let httpMock: HttpTestingController;

  const apiBaseUrl = 'https://api.buddy.test';
  const childId = 'child-1';

  function base(): string {
    return `${apiBaseUrl}/mealplans/children/${childId}`;
  }

  function settings(overrides: Partial<AiProviderSettings> = {}): AiProviderSettings {
    return { providers: [], activeProvider: null, ...overrides };
  }

  function session(overrides: Partial<AiSessionView> = {}): AiSessionView {
    return {
      id: 'session-1',
      from: '2026-08-01',
      to: '2026-08-03',
      requestedSlots: [2],
      status: 0,
      transcript: [],
      draft: [],
      ...overrides
    };
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfigService, useValue: { apiBaseUrl } as Partial<RuntimeConfigService> }
      ]
    });

    service = TestBed.inject(AiAssistantService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('listProviders', () => {
    it('GETs the provider settings for the child', async () => {
      const result = settings({ providers: [{ provider: 0, last4: '1234', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0 });

      const promise = service.listProviders(childId);

      const req = httpMock.expectOne(`${base()}/ai/providers`);
      expect(req.request.method).toBe('GET');
      req.flush(result);

      await expect(promise).resolves.toEqual(result);
    });
  });

  describe('setProviderApiKey', () => {
    it('PUTs the api key to the provider-scoped key endpoint', async () => {
      const promise = service.setProviderApiKey(childId, 0, 'sk-ant-test');

      const req = httpMock.expectOne(`${base()}/ai/providers/0/key`);
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual({ apiKey: 'sk-ant-test' });
      req.flush(settings({ providers: [{ provider: 0, last4: 'test', addedAt: '2026-08-01T00:00:00Z' }], activeProvider: 0 }));

      await expect(promise).resolves.toMatchObject({ activeProvider: 0 });
    });
  });

  describe('removeProviderApiKey', () => {
    it('DELETEs the provider-scoped key endpoint', async () => {
      const promise = service.removeProviderApiKey(childId, 1);

      const req = httpMock.expectOne(`${base()}/ai/providers/1/key`);
      expect(req.request.method).toBe('DELETE');
      req.flush(settings());

      await expect(promise).resolves.toEqual(settings());
    });
  });

  describe('setActiveProvider', () => {
    it('PUTs to the active-provider endpoint with the provider in the URL', async () => {
      const promise = service.setActiveProvider(childId, 2);

      const req = httpMock.expectOne(`${base()}/ai/active-provider/2`);
      expect(req.request.method).toBe('PUT');
      req.flush(settings({ activeProvider: 2 }));

      await expect(promise).resolves.toMatchObject({ activeProvider: 2 });
    });
  });

  describe('testProviderConnection', () => {
    it('POSTs an explicit api key when provided', async () => {
      const promise = service.testProviderConnection(childId, 0, 'sk-ant-candidate');

      const req = httpMock.expectOne(`${base()}/ai/providers/0/test-connection`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ apiKey: 'sk-ant-candidate' });
      req.flush({ isSuccessful: true, errorMessage: null });

      await expect(promise).resolves.toEqual({ isSuccessful: true, errorMessage: null });
    });

    it('POSTs a null api key to test the already-stored key', async () => {
      const promise = service.testProviderConnection(childId, 0);

      const req = httpMock.expectOne(`${base()}/ai/providers/0/test-connection`);
      expect(req.request.body).toEqual({ apiKey: null });
      req.flush({ isSuccessful: false, errorMessage: 'Incorrect API key provided.' });

      await expect(promise).resolves.toEqual({ isSuccessful: false, errorMessage: 'Incorrect API key provided.' });
    });
  });

  describe('getCurrentSession', () => {
    it('GETs the current session', async () => {
      const promise = service.getCurrentSession(childId);

      const req = httpMock.expectOne(`${base()}/ai/sessions/current`);
      expect(req.request.method).toBe('GET');
      req.flush(session());

      await expect(promise).resolves.toEqual(session());
    });

    it('rejects when there is no current session', async () => {
      const promise = service.getCurrentSession(childId);

      const req = httpMock.expectOne(`${base()}/ai/sessions/current`);
      req.flush('not found', { status: 404, statusText: 'Not Found' });

      await expect(promise).rejects.toBeTruthy();
    });
  });

  describe('startSession', () => {
    it('POSTs the session request', async () => {
      const request: StartAiSessionRequest = { from: '2026-08-01', to: '2026-08-03', slots: [2], mustIncludeMealIds: [], notes: null };

      const promise = service.startSession(childId, request);

      const req = httpMock.expectOne(`${base()}/ai/sessions`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(request);
      req.flush(session());

      await expect(promise).resolves.toEqual(session());
    });
  });

  describe('sendMessage', () => {
    it('POSTs the text to the current session messages endpoint', async () => {
      const promise = service.sendMessage(childId, 'Plan five dinners.');

      const req = httpMock.expectOne(`${base()}/ai/sessions/current/messages`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ text: 'Plan five dinners.' });
      req.flush(session());

      await expect(promise).resolves.toEqual(session());
    });
  });

  describe('applyDraft', () => {
    it('POSTs to the apply endpoint', async () => {
      const promise = service.applyDraft(childId);

      const req = httpMock.expectOne(`${base()}/ai/sessions/current/apply`);
      expect(req.request.method).toBe('POST');
      req.flush(session({ status: 1 }));

      await expect(promise).resolves.toMatchObject({ status: 1 });
    });
  });

  describe('discardSession', () => {
    it('POSTs to the discard endpoint', async () => {
      const promise = service.discardSession(childId);

      const req = httpMock.expectOne(`${base()}/ai/sessions/current/discard`);
      expect(req.request.method).toBe('POST');
      req.flush(session({ status: 2 }));

      await expect(promise).resolves.toMatchObject({ status: 2 });
    });
  });
});
