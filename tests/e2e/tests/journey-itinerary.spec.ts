import { test, expect, type Page, type BrowserContext } from '@playwright/test';

import { BASE_URL } from '../support/env.ts';
import { signInAs, TEST_IDENTITIES } from '../support/testAuth.ts';

/** See `journey-1-and-2.spec.ts` for the rationale — direct API mutations need CSRF echoed back. */
async function csrfHeader(page: Page): Promise<{ 'X-CSRF-TOKEN': string }> {
  const response = await page.request.get(`${BASE_URL}/v1/auth/antiforgery`);
  const { token } = await response.json();
  return { 'X-CSRF-TOKEN': token };
}

async function sendInvite(page: Page, email: string, role: 'Editor' | 'Viewer'): Promise<string> {
  await page.getByTestId('invite-email-input').fill(email);
  await page.getByTestId('invite-role-select').click();
  await page.getByRole('option', { name: role }).click();
  await page.getByTestId('send-invite-button').click();

  const invitationRow = page.getByTestId('invitations-list').locator('li', { hasText: email });
  const link = invitationRow.getByTestId('invitation-acceptance-link');
  await expect(link).toBeVisible();
  const href = await link.getAttribute('href');
  if (!href) throw new Error('missing acceptance link');
  return href;
}

/**
 * Journey 5: itinerary + collaborative planning. Reuses `TestAuthEndpoints` (see
 * `journey-1-and-2.spec.ts` for the mechanism). A single serial suite because it stands up a real
 * trip with confirmed dates, invites an Editor and Viewer, then exercises applicability, filters,
 * comments, and activity-feed behaviour on top of that shared state.
 */
test.describe.serial('Journey 5: itinerary + collaborative planning', () => {
  let ownerContext: BrowserContext;
  let ownerPage: Page;
  let tripId: string;
  let editorAcceptanceUrl: string;
  let viewerAcceptanceUrl: string;

  test.beforeAll(async ({ browser }) => {
    ownerContext = await browser.newContext({ ignoreHTTPSErrors: true });
    ownerPage = await ownerContext.newPage();
    await signInAs(ownerPage, TEST_IDENTITIES.owner);
  });

  test.afterAll(async () => {
    await ownerContext.close();
  });

  test('Owner creates a trip with confirmed dates and invites an Editor + Viewer', async () => {
    await ownerPage.goto('/trips/new');
    await ownerPage.getByTestId('trip-name-input').fill('E2E Itinerary Trip');
    await ownerPage.getByTestId('submit-create-trip').click();
    await ownerPage.waitForURL(/\/trips\/[0-9a-fA-F-]{36}$/);
    tripId = new URL(ownerPage.url()).pathname.split('/').pop()!;

    // Confirm dates via the API — the dashboard UI in this slice does not yet expose date confirmation.
    const tripResponse = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}`);
    const trip = await tripResponse.json();
    const updateResp = await ownerPage.request.put(`${BASE_URL}/v1/trips/${tripId}`, {
      data: {
        name: trip.name,
        destinations: trip.destinations,
        reportingCurrencyCode: trip.reportingCurrencyCode,
        dates: { status: 'confirmed', startDate: '2027-05-01', endDate: '2027-05-05' },
        coverImageUrl: trip.coverImageUrl
      },
      headers: { 'If-Match': trip.eTag, ...(await csrfHeader(ownerPage)) }
    });
    expect(updateResp.status()).toBe(200);

    await ownerPage.goto(`/trips/${tripId}/members`);
    editorAcceptanceUrl = await sendInvite(ownerPage, TEST_IDENTITIES.editor.email, 'Editor');
    viewerAcceptanceUrl = await sendInvite(ownerPage, TEST_IDENTITIES.viewer.email, 'Viewer');
  });

  test('Editor accepts and links themselves as a traveller (for applicability)', async ({ browser }) => {
    const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    await signInAs(page, TEST_IDENTITIES.editor, { goToAfterSignIn: editorAcceptanceUrl });
    await page.getByTestId('accept-invitation-button').click();
    await page.waitForURL(`**/trips/${tripId}`);
    await page.getByTestId('add-self-as-traveller').click();
    await expect(page.getByTestId('remove-self-as-traveller')).toBeVisible();
    await ctx.close();
  });

  test('Viewer accepts and links themselves as a traveller', async ({ browser }) => {
    const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    await signInAs(page, TEST_IDENTITIES.viewer, { goToAfterSignIn: viewerAcceptanceUrl });
    await page.getByTestId('accept-invitation-button').click();
    await page.waitForURL(`**/trips/${tripId}`);
    await page.getByTestId('add-self-as-traveller').click();
    await expect(page.getByTestId('remove-self-as-traveller')).toBeVisible();
    await ctx.close();
  });

  test('Owner already has themselves as a traveller (auto-linked on trip creation)', async () => {
    await ownerPage.goto(`/trips/${tripId}`);
    // The trip-creation flow auto-links the creating Owner as a traveller, so the "add" button
    // isn't shown — the "remove" button is instead.
    await expect(ownerPage.getByTestId('remove-self-as-traveller')).toBeVisible();
  });

  test('Editor creates an idea, schedules it, and sees the activity feed update', async ({ browser }) => {
    const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    await signInAs(page, TEST_IDENTITIES.editor, { goToAfterSignIn: `/trips/${tripId}/itinerary` });

    await expect(page.getByTestId('trip-itinerary-page')).toBeVisible();
    await page.getByTestId('idea-title-input').fill('Colosseum tour');
    await page.getByTestId('create-idea-submit').click();

    await expect(page.getByTestId('itinerary-item-title')).toHaveText('Colosseum tour');

    // Schedule onto a confirmed-window date.
    await page.getByTestId('schedule-date-input').fill('2027-05-02');
    await page.getByTestId('schedule-item').click();
    await expect(page.getByText(/Scheduled 2027-05-02/)).toBeVisible();

    // Activity feed shows at least the create + schedule entries.
    const feedItems = page.getByTestId('activity-feed').locator('li');
    await expect.poll(async () => await feedItems.count(), { timeout: 10_000 }).toBeGreaterThanOrEqual(2);

    await ctx.close();
  });

  test('Viewer sees the item, has no mutation controls, but CAN add a comment; direct write attempts are 403', async ({ browser }) => {
    const ctx = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await ctx.newPage();
    await signInAs(page, TEST_IDENTITIES.viewer, { goToAfterSignIn: `/trips/${tripId}/itinerary` });

    await expect(page.getByTestId('itinerary-item-title')).toHaveText('Colosseum tour');
    await expect(page.getByTestId('create-idea-form')).toHaveCount(0);
    await expect(page.getByTestId('schedule-item')).toHaveCount(0);
    await expect(page.getByTestId('delete-item')).toHaveCount(0);

    // Viewer can comment.
    await page.getByTestId('toggle-comments').first().click();
    await page.getByTestId('comment-input').fill('Excited!');
    await page.getByTestId('submit-comment').click();
    await expect(page.getByText('Excited!')).toBeVisible();

    // Server-side proof: raw POST/DELETE for content mutation returns 403.
    const items = await page.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/items`);
    const item = (await items.json())[0];

    const forbiddenCreate = await page.request.post(`${BASE_URL}/v1/trips/${tripId}/itinerary/items`, {
      data: { title: 'Viewer should not be able to add this', notes: null, location: null, applicableTravellerIds: [] },
      headers: await csrfHeader(page)
    });
    expect(forbiddenCreate.status()).toBe(403);

    const forbiddenDelete = await page.request.delete(
      `${BASE_URL}/v1/trips/${tripId}/itinerary/items/${item.id}`,
      { headers: await csrfHeader(page) }
    );
    expect(forbiddenDelete.status()).toBe(403);

    await ctx.close();
  });

  test('Everyone/Me/Selected traveller filter changes visibility for items with applicability set', async () => {
    // Set applicability of the existing item to just the Editor's traveller.
    const travellers = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/travellers`).then(r => r.json());
    const members = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/members`).then(r => r.json());
    const editorMembership = members.find((m: { subjectId: string }) => m.subjectId === TEST_IDENTITIES.editor.subjectId);
    const editorTraveller = travellers.find((t: { linkedMembershipId: string }) => t.linkedMembershipId === editorMembership.id);
    expect(editorTraveller).toBeDefined();

    const items = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/items`).then(r => r.json());
    const item = items[0];
    const setApp = await ownerPage.request.put(
      `${BASE_URL}/v1/trips/${tripId}/itinerary/items/${item.id}/applicability`,
      {
        data: { travellerIds: [editorTraveller.id] },
        headers: { 'If-Match': item.eTag, ...(await csrfHeader(ownerPage)) }
      }
    );
    expect(setApp.status()).toBe(200);

    // Owner (linked traveller = self, NOT the editor's traveller) with Me filter should NOT see the item.
    const filterResp = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/traveller-filter`);
    const filter = await filterResp.json();
    const meResp = await ownerPage.request.put(
      `${BASE_URL}/v1/trips/${tripId}/itinerary/traveller-filter`,
      {
        data: { mode: 'me', selectedTravellerIds: [] },
        headers: { 'If-Match': filter.eTag, ...(await csrfHeader(ownerPage)) }
      }
    );
    expect(meResp.status()).toBe(200);

    const visibleWithMe = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/items`).then(r => r.json());
    expect(visibleWithMe.find((i: { id: string }) => i.id === item.id)).toBeUndefined();

    // Switch back to Everyone: item visible again.
    const filter2 = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/traveller-filter`).then(r => r.json());
    await ownerPage.request.put(
      `${BASE_URL}/v1/trips/${tripId}/itinerary/traveller-filter`,
      {
        data: { mode: 'everyone', selectedTravellerIds: [] },
        headers: { 'If-Match': filter2.eTag, ...(await csrfHeader(ownerPage)) }
      }
    );
    const visibleWithEveryone = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/itinerary/items`).then(r => r.json());
    expect(visibleWithEveryone.find((i: { id: string }) => i.id === item.id)).toBeDefined();
  });

  test('Activity feed grows after comments and edits', async () => {
    await ownerPage.goto(`/trips/${tripId}/itinerary`);
    const feedItems = ownerPage.getByTestId('activity-feed').locator('li');
    await expect.poll(async () => await feedItems.count(), { timeout: 10_000 }).toBeGreaterThanOrEqual(3);
  });
});
