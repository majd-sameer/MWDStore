import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { form, FormField } from '@angular/forms/signals';
import { NgSelectComponent, NgSelectModule } from '@ng-select/ng-select';

/**
 * Guards the load-bearing assumption behind the admin form-control redesign:
 * that ng-select's classic `ControlValueAccessor` interoperates with the
 * experimental Signal Forms `[formField]` directive (same `NG_VALUE_ACCESSOR`
 * path reactive forms uses). If this ever regresses, every `[formField]`
 * ng-select on the form pages silently stops syncing — caught here instead.
 */
@Component({
  selector: 'app-ng-select-ff-host',
  imports: [NgSelectModule, FormField],
  template: `
    <ng-select
      [items]="items"
      bindLabel="name"
      bindValue="id"
      [formField]="f.groupId"
    />
  `,
})
class Host {
  readonly items = [
    { id: '1', name: 'Alpha' },
    { id: '2', name: 'Beta' },
  ];
  readonly model = signal({ groupId: '' });
  readonly f = form(this.model);
}

describe('ng-select ⇄ Signal Forms [formField] interop', () => {
  it('writes the field value into ng-select (field → control)', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    fixture.componentInstance.model.set({ groupId: '2' });
    fixture.detectChanges();

    const select: NgSelectComponent = fixture.debugElement.query(
      By.directive(NgSelectComponent),
    ).componentInstance;
    expect(select.hasValue).toBe(true);
    expect(select.selectedItems[0].value).toEqual({ id: '2', name: 'Beta' });
  });

  it('propagates an ng-select selection into the field (control → field)', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();

    const select: NgSelectComponent = fixture.debugElement.query(
      By.directive(NgSelectComponent),
    ).componentInstance;
    const beta = select.itemsList.items.find((i) => i.value.id === '2')!;
    select.select(beta);
    fixture.detectChanges();

    expect(fixture.componentInstance.model().groupId).toBe('2');
    expect(fixture.componentInstance.f.groupId().value()).toBe('2');
  });
});
