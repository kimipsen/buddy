import type { FlashCardSetKind } from '../../../core/flash-cards.service';

export type MathOperation = 'Addition' | 'Subtraction' | 'Multiplication' | 'Division';

export const MATH_OPERATIONS: readonly MathOperation[] = [
  'Addition',
  'Subtraction',
  'Multiplication',
  'Division',
];

export const KINDS: readonly FlashCardSetKind[] = ['Math', 'Text'];

// MathDeck.IsValidLimit on the backend: sums go up to 100, a table or divisor up to 12.
export function limitRange(operation: MathOperation): { min: number; max: number } {
  return operation === 'Addition' || operation === 'Subtraction'
    ? { min: 2, max: 100 }
    : { min: 1, max: 12 };
}

export function isValidLimit(operation: MathOperation, limit: number): boolean {
  const { min, max } = limitRange(operation);
  return Number.isInteger(limit) && limit >= min && limit <= max;
}

// One card the rule can make, shown while the guardian sets the rule. Not a card the game will
// necessarily draw: it only shows the shape of a card.
export function exampleCard(
  operation: MathOperation,
  limit: number,
): { question: string; answer: string } {
  switch (operation) {
    case 'Addition': {
      const left = Math.ceil(limit / 2);
      return { question: `${left} + ${limit - left}`, answer: String(limit) };
    }
    case 'Subtraction':
      return {
        question: `${limit} − ${Math.floor(limit / 2)}`,
        answer: String(limit - Math.floor(limit / 2)),
      };
    case 'Multiplication':
      return { question: `${limit} × 3`, answer: String(limit * 3) };
    case 'Division':
      return { question: `${limit * 3} ÷ ${limit}`, answer: '3' };
  }
}
