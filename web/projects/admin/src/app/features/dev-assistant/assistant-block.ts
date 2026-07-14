import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import type {
  DevAssistantBlockBase,
  DevAssistantCalloutBlock,
  DevAssistantChecklistBlock,
  DevAssistantEndpointMatrixBlock,
  DevAssistantPropertyGridBlock,
  DevAssistantSuggestionsBlock,
  DevAssistantTextBlock,
} from 'data-access';

/** A run of emphasised/plain text parsed from the server's `**bold**` / `` `code` `` markers. */
export interface TextSegment {
  kind: 'text' | 'strong' | 'code';
  value: string;
}

export function parseEmphasis(text: string): TextSegment[] {
  const segments: TextSegment[] = [];
  // Alternates between plain runs and **strong** / `code` runs; rendered through Angular
  // interpolation only — user-originated strings never reach innerHTML (SEC-6).
  const pattern = /\*\*([^*]+)\*\*|`([^`]+)`/g;
  let last = 0;
  for (const match of text.matchAll(pattern)) {
    if (match.index > last) {
      segments.push({ kind: 'text', value: text.slice(last, match.index) });
    }
    if (match[1] !== undefined) {
      segments.push({ kind: 'strong', value: match[1] });
    } else {
      segments.push({ kind: 'code', value: match[2] });
    }
    last = match.index + match[0].length;
  }
  if (last < text.length) {
    segments.push({ kind: 'text', value: text.slice(last) });
  }
  return segments;
}

/**
 * Renders one reply content block, dispatching on the `type` discriminator (FR-UI-7). An
 * unrecognized discriminator falls back to the block's `summary`, which every block carries
 * precisely for this purpose.
 */
@Component({
  selector: 'app-dev-assistant-block',
  imports: [TranslatePipe],
  templateUrl: './assistant-block.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminDevAssistantBlock {
  readonly block = input.required<DevAssistantBlockBase>();
  /** Checked step indexes for a checklist block (visual progress only, FR-UI-10). */
  readonly checked = input<ReadonlySet<number>>(new Set());

  readonly toggleStep = output<number>();
  readonly run = output<string>();

  protected readonly gridFilter = signal('');
  protected readonly matrixFilter = signal('');
  protected readonly matrixVerb = signal('');
  protected readonly copiedText = signal<string | null>(null);

  protected asText(block: DevAssistantBlockBase): DevAssistantTextBlock {
    return block as DevAssistantTextBlock;
  }
  protected asChecklist(block: DevAssistantBlockBase): DevAssistantChecklistBlock {
    return block as DevAssistantChecklistBlock;
  }
  protected asGrid(block: DevAssistantBlockBase): DevAssistantPropertyGridBlock {
    return block as DevAssistantPropertyGridBlock;
  }
  protected asMatrix(block: DevAssistantBlockBase): DevAssistantEndpointMatrixBlock {
    return block as DevAssistantEndpointMatrixBlock;
  }
  protected asCallout(block: DevAssistantBlockBase): DevAssistantCalloutBlock {
    return block as DevAssistantCalloutBlock;
  }
  protected asSuggestions(block: DevAssistantBlockBase): DevAssistantSuggestionsBlock {
    return block as DevAssistantSuggestionsBlock;
  }

  protected readonly segments = computed<TextSegment[]>(() => {
    const block = this.block();
    return block.type === 'text' ? parseEmphasis(this.asText(block).text) : [];
  });

  protected readonly gridRows = computed(() => {
    const block = this.block();
    if (block.type !== 'propertyGrid') return [];
    const filter = this.gridFilter().trim().toLowerCase();
    const rows = this.asGrid(block).rows;
    return filter
      ? rows.filter((row) => row.name.toLowerCase().includes(filter))
      : rows;
  });

  protected readonly matrixRows = computed(() => {
    const block = this.block();
    if (block.type !== 'endpointMatrix') return [];
    const filter = this.matrixFilter().trim().toLowerCase();
    const verb = this.matrixVerb();
    return this.asMatrix(block).rows.filter(
      (row) =>
        (!verb || row.verb === verb) &&
        (!filter ||
          row.route.toLowerCase().includes(filter) ||
          row.action.toLowerCase().includes(filter)),
    );
  });

  protected readonly matrixVerbs = computed(() => {
    const block = this.block();
    if (block.type !== 'endpointMatrix') return [];
    return [...new Set(this.asMatrix(block).rows.map((row) => row.verb))].sort();
  });

  protected readonly checkedCount = computed(() => {
    const block = this.block();
    if (block.type !== 'checklist') return 0;
    const total = this.asChecklist(block).steps.length;
    return [...this.checked()].filter((index) => index < total).length;
  });

  protected layerClass(layer: string): string {
    return 'layer-' + layer.toLowerCase().replace(/[^a-z]+/g, '-');
  }

  protected async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copiedText.set(text);
      setTimeout(() => {
        if (this.copiedText() === text) this.copiedText.set(null);
      }, 1500);
    } catch {
      // Clipboard unavailable (permissions / non-secure context) — the affordance is best-effort.
    }
  }
}
