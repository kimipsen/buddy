import { HttpErrorResponse } from '@angular/common/http';

import { isEmailNotVerified } from './email-not-verified';

describe('isEmailNotVerified', () => {
  it('is true for a 403 with the email_not_verified code', () => {
    const error = new HttpErrorResponse({ status: 403, error: { code: 'email_not_verified' } });

    expect(isEmailNotVerified(error)).toBe(true);
  });

  it('is false for a plain 403', () => {
    expect(isEmailNotVerified(new HttpErrorResponse({ status: 403 }))).toBe(false);
  });

  it('is false for another 403 code', () => {
    const error = new HttpErrorResponse({ status: 403, error: { code: 'user_not_provisioned' } });

    expect(isEmailNotVerified(error)).toBe(false);
  });

  it('is false for the code on another status', () => {
    const error = new HttpErrorResponse({ status: 400, error: { code: 'email_not_verified' } });

    expect(isEmailNotVerified(error)).toBe(false);
  });

  it('is false for an error that is not an HTTP error', () => {
    expect(isEmailNotVerified(new Error('email_not_verified'))).toBe(false);
  });
});
