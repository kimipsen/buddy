import { request } from '@playwright/test';

import { getAccessToken } from './keycloak-client';
import { readRuntimeConfig } from './runtime-config';
import type { TestUser } from './seeded-users';

// OnboardingStatus names as the API writes them (ONBOARDING_STATUS in src/app/core/onboarding.service.ts).
const NOT_STARTED = 'NotStarted';
const ACTIVE = 'Active';
const DEFERRED = 'Deferred';

interface OnboardingProgressDto {
  status: 'NotStarted' | 'Active' | 'Deferred' | 'Completed';
  setupGroupId: string | null;
  invitationsSkipped: boolean;
  version: number;
}

// A guardian with no groups or children is sent into the guided setup
// (docs/frontend/analysis/guardian-onboarding.md) instead of the dashboard. Specs about other
// features start from the dashboard, so the fixtures put the guide aside the way "Finish later"
// does: provision the user (GET /users/me), then store a deferred guide. Only when the app would
// redirect (the same rule as OnboardingService.shouldEnterGuide): a guardian who already has a
// group or a child, or whose guide is deferred or completed, is left alone and keeps a dashboard
// without a "Resume setup" card.
export async function deferOnboarding(user: TestUser): Promise<void> {
  const { accessToken } = await getAccessToken(user.username, user.password);
  const { apiBaseUrl } = readRuntimeConfig();
  const api = await request.newContext({
    baseURL: apiBaseUrl,
    ignoreHTTPSErrors: true,
    extraHTTPHeaders: { Authorization: `Bearer ${accessToken}` },
  });

  try {
    const provisioned = await api.get('/users/me');
    if (!provisioned.ok()) {
      throw new Error(
        `provisioning ${user.username} failed: ${provisioned.status()} ${await provisioned.text()}`,
      );
    }

    const current = (await (await api.get('/users/me/onboarding')).json()) as OnboardingProgressDto;
    if (current.status === NOT_STARTED) {
      const groups = (await (await api.get('/groups')).json()) as unknown[];
      const children = (await (await api.get('/users/me/children')).json()) as unknown[];
      if (groups.length > 0 || children.length > 0) {
        return;
      }
    } else if (current.status !== ACTIVE) {
      return;
    }

    const deferred = await api.put('/users/me/onboarding', {
      data: { ...current, status: DEFERRED },
    });
    if (!deferred.ok()) {
      throw new Error(`deferring onboarding for ${user.username} failed: ${deferred.status()}`);
    }
  } finally {
    await api.dispose();
  }
}
