import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import type {
  DevAssistantBlockBase,
  DevAssistantChecklistBlock,
  DevAssistantPropertyGridBlock,
  DevAssistantSuggestionsBlock,
} from 'data-access';
import { AdminDevAssistantBlock, parseEmphasis } from './assistant-block';

describe('AdminDevAssistantBlock', () => {
  let fixture: ComponentFixture<AdminDevAssistantBlock>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminDevAssistantBlock],
      providers: [provideTranslateService()],
    }).compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      devAssistant: {
        progress: '{{done}} of {{total}}',
        grid: { bilingual: 'bilingual overlay' },
        fallbackNote: 'This reply uses a newer block type',
      },
    });
    translate.use('en');
    fixture = TestBed.createComponent(AdminDevAssistantBlock);
  });

  async function render(block: DevAssistantBlockBase): Promise<HTMLElement> {
    fixture.componentRef.setInput('block', block);
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('dispatches a propertyGrid block to the grid renderer', async () => {
    const block: DevAssistantPropertyGridBlock = {
      type: 'propertyGrid',
      summary: 'Category (Category), 2 column(s)',
      entityName: 'Category',
      tableName: 'Category',
      markers: ['ISoftDeletable'],
      isBilingual: true,
      rows: [
        {
          name: 'Id',
          clrType: 'Int64',
          sqlType: 'bigint',
          maxLength: null,
          nullable: false,
          defaultValue: null,
          isPrimaryKey: true,
          isForeignKey: false,
          foreignKeyPrincipal: null,
          isIndexed: false,
          isUnique: false,
          isSensitive: false,
        },
      ],
      relations: [],
    };

    const el = await render(block);

    expect(el.querySelector('.da-grid')).toBeTruthy();
    expect(el.textContent).toContain('Category');
    expect(el.textContent).toContain('bilingual overlay');
    expect(el.querySelector('.da-pill-pk')).toBeTruthy();
  });

  it('renders the summary fallback for an unknown block type', async () => {
    const el = await render({
      type: 'hologram',
      summary: 'Something from a newer server',
    });

    expect(el.querySelector('.da-fallback')).toBeTruthy();
    expect(el.textContent).toContain('Something from a newer server');
    expect(el.textContent).toContain('hologram');
  });

  it('emits toggleStep with the step index and reflects checked state', async () => {
    const block: DevAssistantChecklistBlock = {
      type: 'checklist',
      summary: 'Add a field to Category',
      title: 'Add a field to Category',
      interactive: true,
      steps: [
        {
          layer: 'Domain',
          filePath: 'Store.Domain/Category.cs',
          verified: true,
          description: 'Add the property.',
          command: null,
          warnings: [],
        },
        {
          layer: 'Data',
          filePath: 'Store.Data/Configurations/CategoryConfiguration.cs',
          verified: true,
          description: 'Map it.',
          command: null,
          warnings: [],
        },
      ],
    };

    fixture.componentRef.setInput('checked', new Set([0]));
    const el = await render(block);

    const emitted: number[] = [];
    fixture.componentInstance.toggleStep.subscribe((index) => emitted.push(index));

    const checkboxes = el.querySelectorAll<HTMLInputElement>('.da-check');
    expect(checkboxes.length).toBe(2);
    expect(checkboxes[0].checked).toBe(true);
    expect(checkboxes[1].checked).toBe(false);
    expect(el.textContent).toContain('1 of 2');

    checkboxes[1].dispatchEvent(new Event('change'));
    expect(emitted).toEqual([1]);
  });

  it('re-submits a suggestion chip query through the run output', async () => {
    const block: DevAssistantSuggestionsBlock = {
      type: 'suggestions',
      summary: 'Did you mean',
      items: [{ label: 'CustomerGroup', query: 'Show me all routes for customer-groups' }],
    };

    const el = await render(block);
    const queries: string[] = [];
    fixture.componentInstance.run.subscribe((query) => queries.push(query));

    el.querySelector<HTMLButtonElement>('.da-chip')!.click();
    expect(queries).toEqual(['Show me all routes for customer-groups']);
  });
});

describe('parseEmphasis', () => {
  it('splits **strong** and `code` runs while keeping plain text', () => {
    expect(parseEmphasis('Columns of **Category** (table `Category`).')).toEqual([
      { kind: 'text', value: 'Columns of ' },
      { kind: 'strong', value: 'Category' },
      { kind: 'text', value: ' (table ' },
      { kind: 'code', value: 'Category' },
      { kind: 'text', value: ').' },
    ]);
  });

  it('passes through text with no markers', () => {
    expect(parseEmphasis('plain')).toEqual([{ kind: 'text', value: 'plain' }]);
  });
});
