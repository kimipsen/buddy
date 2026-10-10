import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { postIdempotent } from './http-idempotency';
import type { Schemas } from './api/schemas';
import { RuntimeConfigService } from './runtime-config.service';

// One rule book (ListRules and every write that returns the book): the caller's tier, the
// children whose acknowledgements it tracks, and the ordered rules with each child's status.
export type RuleBook = Schemas['RuleBookResponse'];
export type Rule = Schemas['RuleResponse'];
export type RuleAcknowledgement = Schemas['RuleAcknowledgementResponse'];
export type HouseRulesAccess = Schemas['HouseRulesAccessTier'];

// Everything one child is asked to keep: personal rules plus one section per household.
export type ChildRules = Schemas['ChildRulesResponse'];
export type ChildRuleSection = Schemas['ChildRuleSectionResponse'];
export type ChildRule = Schemas['ChildRuleResponse'];

export type RuleBookScopeKind = Schemas['RuleBookScopeKind'];

// Which book a call addresses: a child's personal rules or a household group's.
export interface RuleScope {
  kind: RuleBookScopeKind;
  id: string;
}

export interface RuleContent {
  title: string;
  body: string;
}

// The API's answer when a child acknowledges a revision that a normal edit has since replaced.
export const REVISION_CHANGED_STATUS = 409;

export function isRevisionChanged(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === REVISION_CHANGED_STATUS;
}

export function scopeOf(section: Pick<ChildRuleSection, 'scopeKind' | 'scopeId'>): RuleScope {
  return { kind: section.scopeKind, id: section.scopeId };
}

@Injectable({ providedIn: 'root' })
export class HouseRulesService {
  private readonly http = inject(HttpClient);
  private readonly runtimeConfig = inject(RuntimeConfigService);

  private rules(scope: RuleScope): string {
    const segment = scope.kind === 'Child' ? 'children' : 'groups';
    return `${this.runtimeConfig.apiBaseUrl}/house-rules/${segment}/${scope.id}/rules`;
  }

  listRules(scope: RuleScope): Promise<RuleBook> {
    return firstValueFrom(this.http.get<RuleBook>(this.rules(scope)));
  }

  addRule(scope: RuleScope, content: RuleContent): Promise<RuleBook> {
    return firstValueFrom(postIdempotent<RuleBook>(this.http, this.rules(scope), content));
  }

  editRule(
    scope: RuleScope,
    ruleId: string,
    content: RuleContent,
    requireReacknowledgement: boolean,
  ): Promise<RuleBook> {
    return firstValueFrom(
      this.http.put<RuleBook>(`${this.rules(scope)}/${ruleId}`, {
        ...content,
        requireReacknowledgement,
      }),
    );
  }

  removeRule(scope: RuleScope, ruleId: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.rules(scope)}/${ruleId}`));
  }

  reorderRules(scope: RuleScope, newOrder: string[]): Promise<RuleBook> {
    return firstValueFrom(this.http.put<RuleBook>(`${this.rules(scope)}/order`, { newOrder }));
  }

  // childId only when a guardian acknowledges on a child's behalf; a child leaves it out.
  acknowledge(scope: RuleScope, ruleId: string, revision: number, childId?: string): Promise<void> {
    const body = childId === undefined ? { revision } : { revision, childId };
    return firstValueFrom(
      this.http.put<void>(`${this.rules(scope)}/${ruleId}/acknowledgement`, body),
    );
  }

  getChildRules(childId: string): Promise<ChildRules> {
    return firstValueFrom(
      this.http.get<ChildRules>(`${this.runtimeConfig.apiBaseUrl}/house-rules/children/${childId}`),
    );
  }
}
