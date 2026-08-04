import { test, expect, type Page, type BrowserContext } from '@playwright/test';

import { BASE_URL } from '../support/env.ts';
import { signInAs, TEST_IDENTITIES } from '../support/testAuth.ts';

/**
 * Core Journey 1 ("start a trip") + Journey 2 ("plan together": membership, roles, invitations)
 * flow, driven entirely through the UI plus a handful of direct `/v1` API calls used only to prove
 * that role authorization is enforced server-side (not just hidden in the UI). Each identity gets
 * its own isolated `BrowserContext` (its own cookie jar), since `TestAuthEndpoints` signs a request
 * in via a cookie - reusing one context across identities would mix sessions.
 *
 * Runs as a single serial suite because later steps (accepting invitations, checking roles) depend
 * on state created by earlier steps (the trip id, the stubbed acceptance links).
 */
test.describe.serial('Journey 1 & 2: start a trip, invite, accept, enforce roles', () => {
  let ownerContext: BrowserContext;
  let ownerPage: Page;
  let tripId: string;
  let editorAcceptanceUrl: string;
  let viewerAcceptanceUrl: string;
  let mismatchAcceptanceUrl: string;

  const mismatchInvitedEmail = 'not-invited-in-time@e2e.tripsidekick.test';

  test.beforeAll(async ({ browser }) => {
    ownerContext = await browser.newContext({ ignoreHTTPSErrors: true });
    ownerPage = await ownerContext.newPage();
    await signInAs(ownerPage, TEST_IDENTITIES.owner);
  });

  test.afterAll(async () => {
    await ownerContext.close();
  });

  test('Owner starts a trip with just a name', async () => {
    await ownerPage.goto('/trips/new');
    await ownerPage.getByTestId('trip-name-input').fill('E2E Round-the-world Trip');
    await ownerPage.getByTestId('submit-create-trip').click();

    await ownerPage.waitForURL(/\/trips\/[0-9a-fA-F-]{36}$/);
    tripId = new URL(ownerPage.url()).pathname.split('/').pop()!;

    await expect(ownerPage.getByTestId('trip-dashboard-page')).toBeVisible();
    await expect(ownerPage.getByTestId('trip-name')).toHaveText('E2E Round-the-world Trip');
    // Undecided dates: the setup-completeness banner and manage-members link (Owner-only) both show.
    await expect(ownerPage.getByTestId('dates-not-confirmed-banner')).toBeVisible();
    await expect(ownerPage.getByTestId('manage-members-link')).toBeVisible();
  });

  test('Owner invites an Editor, a Viewer, and a third invitee (for the mismatched-identity test)', async () => {
    await ownerPage.goto(`/trips/${tripId}/members`);
    await expect(ownerPage.getByTestId('manage-members-page')).toBeVisible();

    editorAcceptanceUrl = await sendInvite(ownerPage, TEST_IDENTITIES.editor.email, 'Editor');
    viewerAcceptanceUrl = await sendInvite(ownerPage, TEST_IDENTITIES.viewer.email, 'Viewer');
    mismatchAcceptanceUrl = await sendInvite(ownerPage, mismatchInvitedEmail, 'Viewer');

    expect(editorAcceptanceUrl).toContain('/invitations/accept?token=');
    expect(viewerAcceptanceUrl).toContain('/invitations/accept?token=');
    expect(mismatchAcceptanceUrl).toContain('/invitations/accept?token=');

    const invitationsList = ownerPage.getByTestId('invitations-list');
    await expect(invitationsList.getByText(TEST_IDENTITIES.editor.email)).toBeVisible();
    await expect(invitationsList.getByText(TEST_IDENTITIES.viewer.email)).toBeVisible();
    await expect(invitationsList.getByText(mismatchInvitedEmail)).toBeVisible();
  });

  test('Editor accepts their invitation and gains the Editor role', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await signInAs(page, TEST_IDENTITIES.editor, { goToAfterSignIn: editorAcceptanceUrl });

    await expect(page.getByTestId('accept-invitation-page')).toBeVisible();
    await page.getByTestId('accept-invitation-button').click();

    await page.waitForURL(`**/trips/${tripId}`);
    await expect(page.getByTestId('trip-dashboard-page')).toBeVisible();

    const members = await page.request.get(`${BASE_URL}/v1/trips/${tripId}/members`);
    const editorMembership = (await members.json()).find(
      (member: { subjectId: string }) => member.subjectId === TEST_IDENTITIES.editor.subjectId
    );
    expect(editorMembership?.role).toBe(1); // MembershipRole.Editor

    await context.close();
  });

  test('Viewer accepts their invitation and gains the Viewer role', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await signInAs(page, TEST_IDENTITIES.viewer, { goToAfterSignIn: viewerAcceptanceUrl });

    await page.getByTestId('accept-invitation-button').click();
    await page.waitForURL(`**/trips/${tripId}`);

    const members = await page.request.get(`${BASE_URL}/v1/trips/${tripId}/members`);
    const viewerMembership = (await members.json()).find(
      (member: { subjectId: string }) => member.subjectId === TEST_IDENTITIES.viewer.subjectId
    );
    expect(viewerMembership?.role).toBe(0); // MembershipRole.Viewer

    await context.close();
  });

  test('a mismatched identity cannot accept an invitation bound to a different email', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    // Signed in as a real, distinct identity - but NOT the email the invitation was sent to.
    await signInAs(page, TEST_IDENTITIES.mismatched, { goToAfterSignIn: mismatchAcceptanceUrl });

    await page.getByTestId('accept-invitation-button').click();

    await expect(page.getByTestId('accept-invitation-error')).toBeVisible();
    // Never actually joined the trip. Use the Owner's session to check membership: the mismatched
    // identity was correctly refused membership, so a members-list request *as that identity*
    // would itself 403 (it was never granted access) rather than proving anything useful here.
    const members = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/members`);
    const asMismatched = (await members.json()).find(
      (member: { subjectId: string }) => member.subjectId === TEST_IDENTITIES.mismatched.subjectId
    );
    expect(asMismatched).toBeUndefined();

    await context.close();
  });

  test('Editor can edit trip content but cannot manage membership', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await signInAs(page, TEST_IDENTITIES.editor, { goToAfterSignIn: `/trips/${tripId}` });

    await expect(page.getByTestId('edit-trip-name')).toBeVisible();
    await expect(page.getByTestId('manage-members-link')).toHaveCount(0);

    // Editors can edit trip content: rename it, and confirm the change is visible.
    await page.getByTestId('edit-trip-name').click();
    await page.getByTestId('trip-name-input').fill('E2E Round-the-world Trip (renamed by Editor)');
    await page.getByTestId('save-trip-name').click();
    await expect(page.getByTestId('trip-name')).toHaveText('E2E Round-the-world Trip (renamed by Editor)');

    // Server-side proof, not just "the button isn't shown": a direct role-change attempt is 403.
    const members = await page.request.get(`${BASE_URL}/v1/trips/${tripId}/members`);
    const viewerMembership = (await members.json()).find(
      (member: { subjectId: string }) => member.subjectId === TEST_IDENTITIES.viewer.subjectId
    );

    const forbidden = await page.request.put(
      `${BASE_URL}/v1/trips/${tripId}/members/${viewerMembership.id}/role`,
      { data: { role: 1 }, headers: { 'If-Match': viewerMembership.eTag } }
    );
    expect(forbidden.status()).toBe(403);

    await context.close();
  });

  test('Viewer is read-only: cannot edit trip content or manage membership', async ({ browser }) => {
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    await signInAs(page, TEST_IDENTITIES.viewer, { goToAfterSignIn: `/trips/${tripId}` });

    await expect(page.getByTestId('edit-trip-name')).toHaveCount(0);
    await expect(page.getByTestId('manage-members-link')).toHaveCount(0);

    const tripResponse = await page.request.get(`${BASE_URL}/v1/trips/${tripId}`);
    const trip = await tripResponse.json();

    const forbiddenEdit = await page.request.put(`${BASE_URL}/v1/trips/${tripId}`, {
      data: {
        name: 'Viewer should not be able to set this name',
        destinations: trip.destinations,
        reportingCurrencyCode: trip.reportingCurrencyCode,
        dates: trip.dates,
        coverImageUrl: trip.coverImageUrl
      },
      headers: { 'If-Match': trip.eTag }
    });
    expect(forbiddenEdit.status()).toBe(403);

    await context.close();
  });

  test('last-owner protection: the sole Owner cannot leave, be removed, or be demoted', async () => {
    await ownerPage.goto(`/trips/${tripId}`);
    await ownerPage.getByTestId('leave-trip-button').click();
    await expect(ownerPage.getByRole('alert')).toBeVisible();

    // Still an Owner and still a member after the failed leave attempt.
    const membersAfterFailedLeave = await ownerPage.request.get(`${BASE_URL}/v1/trips/${tripId}/members`);
    const ownerMembership = (await membersAfterFailedLeave.json()).find(
      (member: { subjectId: string }) => member.subjectId === TEST_IDENTITIES.owner.subjectId
    );
    expect(ownerMembership?.role).toBe(2); // MembershipRole.Owner

    await ownerPage.goto(`/trips/${tripId}/members`);
    // The sole Owner's own row has no remove button (see ManageMembersPage's secondaryAction rule).
    await expect(ownerPage.getByTestId(`remove-member-${ownerMembership.id}`)).toHaveCount(0);

    // Server-side proof: attempting to demote the last Owner via the API is refused (409).
    const demote = await ownerPage.request.put(
      `${BASE_URL}/v1/trips/${tripId}/members/${ownerMembership.id}/role`,
      { data: { role: 1 }, headers: { 'If-Match': ownerMembership.eTag } }
    );
    expect(demote.status()).toBe(409);

    // ... and removal is refused the same way.
    const remove = await ownerPage.request.delete(`${BASE_URL}/v1/trips/${tripId}/members/${ownerMembership.id}`);
    expect(remove.status()).toBe(409);
  });
});

/** Fills and submits the ManageMembersPage invite form; returns the stubbed acceptance link's href. */
async function sendInvite(page: Page, email: string, role: 'Editor' | 'Viewer'): Promise<string> {
  await page.getByTestId('invite-email-input').fill(email);
  await page.getByTestId('invite-role-select').click();
  await page.getByRole('option', { name: role }).click();
  await page.getByTestId('send-invite-button').click();

  const invitationRow = page.getByTestId('invitations-list').locator('li', { hasText: email });
  const link = invitationRow.getByTestId('invitation-acceptance-link');
  await expect(link).toBeVisible();

  const href = await link.getAttribute('href');
  if (!href) {
    throw new Error(`Invitation acceptance link for ${email} had no href.`);
  }

  return href;
}
