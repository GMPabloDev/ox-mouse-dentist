import { ChangeDetectionStrategy, Component } from '@angular/core';
import type { ComponentFixture } from '@angular/core/testing';
import { TestBed } from '@angular/core/testing';
import { PublicShell } from './public-shell';

@Component({
  selector: 'app-public-shell-host',
  imports: [PublicShell],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-public-shell title="Tu acceso, en tus manos." subtitle="Elige una contraseña.">
      <form id="formulario-publico"></form>
    </app-public-shell>
  `,
})
class PublicShellHost {}

async function render(): Promise<ComponentFixture<PublicShellHost>> {
  TestBed.configureTestingModule({ imports: [PublicShellHost] });
  const fixture = TestBed.createComponent(PublicShellHost);
  await fixture.whenStable();

  return fixture;
}

function host(fixture: ComponentFixture<unknown>): HTMLElement {
  return fixture.nativeElement as HTMLElement;
}

function text(fixture: ComponentFixture<unknown>): string {
  return host(fixture).textContent ?? '';
}

/**
 * jsdom no aplica hoy las hojas de Tailwind, así que el contrato responsive se
 * comprueba sobre las clases que deciden qué se ve en cada ancho.
 */
function classes(element: Element): string[] {
  return Array.from(element.classList);
}

function narrativeTitle(fixture: ComponentFixture<unknown>): HTMLElement {
  return host(fixture).querySelector<HTMLElement>('aside h2')!;
}

function narrativeFooter(fixture: ComponentFixture<unknown>): HTMLElement {
  const paragraphs = [...host(fixture).querySelectorAll<HTMLElement>('aside p')];
  const footer = paragraphs.find((paragraph) =>
    paragraph.textContent?.includes('Acceso exclusivo para el personal de MouseDent'),
  );

  if (!footer) {
    throw new Error('No se encontró el pie del panel narrativo');
  }

  return footer;
}

describe('PublicShell', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('compone el relato de acceso exclusivo alrededor del formulario', async () => {
    const fixture = await render();

    expect(text(fixture)).toContain('Tu acceso, en tus manos.');
    expect(text(fixture)).toContain('Elige una contraseña.');
    expect(text(fixture)).toContain('Acceso exclusivo para el personal de MouseDent');
    expect(text(fixture)).toContain('MouseDent');
    expect(host(fixture).querySelector('main form#formulario-publico')).not.toBeNull();
  });

  it('oculta el relato por debajo de 741 px y conserva la marca', async () => {
    const fixture = await render();
    const main = host(fixture).querySelector<HTMLElement>('main')!;
    const brand = host(fixture).querySelector<HTMLElement>('aside > div')!;

    expect(classes(main)).toEqual(
      expect.arrayContaining(['grid-cols-1', 'min-[741px]:grid-cols-2']),
    );
    expect(classes(narrativeTitle(fixture).parentElement!)).toEqual(
      expect.arrayContaining(['hidden', 'min-[741px]:block']),
    );
    expect(classes(narrativeFooter(fixture))).toEqual(
      expect.arrayContaining(['hidden', 'min-[741px]:flex']),
    );
    expect(classes(brand)).not.toContain('hidden');
  });
});
