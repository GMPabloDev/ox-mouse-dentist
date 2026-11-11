import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form, type FieldTree } from '@angular/forms/signals';
import type { LocalResponse, LocalScheduleDay } from '@mousedent/api-client';
import {
  checkPhotoFile,
  draftFrom,
  emptyDraft,
  mapValidationErrors,
  scheduleJson,
  toCreateForm,
  toUpdateForm,
  type LocalDraft,
} from './local-form';
import { localFormSchema } from './local-form-schema';
import { applyRange, closedWeek, WEEK_DAYS } from './schedule-model';

const LOCAL: LocalResponse = {
  id: 'local-1',
  name: 'MouseDent Miraflores',
  address: 'Av. José Pardo 620, Miraflores',
  phone: '(01) 555 0120',
  isActive: true,
  schedule: applyRange(closedWeek(), new Set(WEEK_DAYS.slice(0, 6)), '09:00', '18:00'),
  photo: { contentType: 'image/png', version: 'foto-1.png' },
};

function fileOf(type: string, size: number, name = 'foto'): File {
  return { name, type, size } as File;
}

function openMonday(opensAt: string, closesAt: string): LocalScheduleDay[] {
  return applyRange(closedWeek(), new Set(['Monday']), opensAt, closesAt);
}

/** El esquema se prueba sobre el árbol de Signal Forms real, no sobre copias. */
function formOf(model: LocalDraft): FieldTree<LocalDraft> {
  return TestBed.runInInjectionContext(() => form(signal(model), localFormSchema));
}

function messagesOf(field: () => readonly { message?: string }[]): string[] {
  return field().map((error) => error.message ?? '');
}

describe('datos del formulario de locales', () => {
  it('exige nombre, dirección, teléfono y al menos un día abierto', () => {
    const localForm = formOf(emptyDraft());

    expect(messagesOf(localForm.name().errors)).toEqual(['El nombre es obligatorio.']);
    expect(messagesOf(localForm.address().errors)).toEqual(['La dirección es obligatoria.']);
    expect(messagesOf(localForm.phone().errors)).toEqual(['El teléfono es obligatorio.']);
    expect(messagesOf(localForm.schedule().errors)).toEqual([
      'El horario debe tener al menos un día abierto.',
    ]);
    expect(localForm().valid()).toBe(false);
  });

  it('no acepta un nombre ni una dirección de solo espacios', () => {
    const localForm = formOf({ ...emptyDraft(), name: '   ', address: '  ' });

    expect(messagesOf(localForm.name().errors)).toEqual(['El nombre es obligatorio.']);
    expect(messagesOf(localForm.address().errors)).toEqual(['La dirección es obligatoria.']);
  });

  it('acepta un local con contacto y una semana abierta', () => {
    const localForm = formOf({
      name: 'MouseDent Miraflores',
      address: 'Av. José Pardo 620',
      phone: '555 0120',
      schedule: applyRange(closedWeek(), new Set(['Monday']), '09:00', '18:00'),
    });

    expect(messagesOf(localForm.name().errors)).toEqual([]);
    expect(messagesOf(localForm.address().errors)).toEqual([]);
    expect(messagesOf(localForm.phone().errors)).toEqual([]);
    expect(messagesOf(localForm.schedule().errors)).toEqual([]);
    expect(localForm().valid()).toBe(true);
  });

  it('señala el día abierto cuya apertura no precede al cierre', () => {
    const localForm = formOf({
      name: 'MouseDent',
      address: 'Av. Siempre Viva 1',
      phone: '555',
      schedule: openMonday('18:00', '09:00'),
    });

    expect(messagesOf(localForm.schedule[0]().errors)).toEqual([
      'La hora de cierre debe ser posterior a la hora de apertura.',
    ]);
    expect(messagesOf(localForm.schedule().errors)).toEqual([
      'Revisa los días abiertos del horario.',
    ]);
  });

  it('respeta los límites de longitud de la API', () => {
    const localForm = formOf({
      name: 'a'.repeat(121),
      address: 'b'.repeat(201),
      phone: 'c'.repeat(41),
      schedule: openMonday('09:00', '18:00'),
    });

    expect(messagesOf(localForm.name().errors)).toEqual([
      'El nombre no puede superar los 120 caracteres.',
    ]);
    expect(messagesOf(localForm.address().errors)).toEqual([
      'La dirección no puede superar los 200 caracteres.',
    ]);
    expect(messagesOf(localForm.phone().errors)).toEqual([
      'El teléfono no puede superar los 40 caracteres.',
    ]);
  });

  it('acepta JPG, PNG y WebP de hasta 5 MB y rechaza el resto', () => {
    expect(checkPhotoFile(fileOf('image/png', 1024))).toBeNull();
    expect(checkPhotoFile(fileOf('image/jpeg', 5 * 1024 * 1024))).toBeNull();
    expect(checkPhotoFile(fileOf('image/webp', 2048))).toBeNull();
    expect(checkPhotoFile(fileOf('image/gif', 1024))).toBe(
      'Solo se aceptan imágenes JPG, PNG o WebP.',
    );
    expect(checkPhotoFile(fileOf('image/png', 5 * 1024 * 1024 + 1))).toBe(
      'La foto no puede superar 5 MB.',
    );
    expect(checkPhotoFile(fileOf('image/png', 0))).toBe('La foto está vacía.');
  });

  it('construye el alta con los siete días en JSON y sin foto cuando no se eligió', () => {
    const form = toCreateForm(
      {
        name: '  MouseDent Miraflores ',
        address: ' Av. José Pardo 620 ',
        phone: ' 555 0120 ',
        schedule: applyRange(closedWeek(), new Set(['Monday']), '09:00', '18:00'),
      },
      null,
    );
    const schedule = JSON.parse(form.schedule ?? '') as LocalScheduleDay[];

    expect(form.name).toBe('MouseDent Miraflores');
    expect(form.address).toBe('Av. José Pardo 620');
    expect(form.phone).toBe('555 0120');
    expect(schedule).toHaveLength(7);
    expect(schedule.map((day) => day.day)).toEqual([...WEEK_DAYS]);
    expect(schedule[0]).toEqual({
      day: 'Monday',
      isClosed: false,
      opensAt: '09:00',
      closesAt: '18:00',
    });
    expect(schedule[6]).toEqual({
      day: 'Sunday',
      isClosed: true,
      opensAt: null,
      closesAt: null,
    });
    expect('photo' in form).toBe(false);
  });

  it('incluye la foto elegida en el alta y completa los días ausentes', () => {
    const photo = fileOf('image/png', 900, 'frente.png');
    const form = toCreateForm(
      { name: 'MouseDent', address: 'Av. 1', phone: '555', schedule: openMonday('09:00', '18:00') },
      photo,
    );
    const schedule = JSON.parse(form.schedule ?? '') as LocalScheduleDay[];

    expect(form.photo).toBe(photo);
    expect(schedule).toHaveLength(7);
    expect(schedule[1]).toEqual({
      day: 'Tuesday',
      isClosed: true,
      opensAt: null,
      closesAt: null,
    });
  });

  it('indica de forma explícita qué pasa con la foto guardada al editar', () => {
    const draft = draftFrom(LOCAL);
    const photo = fileOf('image/webp', 2048, 'nueva.webp');

    expect(toUpdateForm(draft, 'keep', null).photoAction).toBe('keep');
    expect('photo' in toUpdateForm(draft, 'keep', null)).toBe(false);
    expect(toUpdateForm(draft, 'remove', null).photoAction).toBe('remove');
    expect(toUpdateForm(draft, 'replace', photo).photo).toBe(photo);
    expect(toUpdateForm(draft, 'replace', photo).photoAction).toBe('replace');
  });

  it('serializa el horario del local guardado sin perder su orden', () => {
    const schedule = JSON.parse(scheduleJson(LOCAL.schedule)) as LocalScheduleDay[];

    expect(schedule).toHaveLength(7);
    expect(schedule[5]).toEqual({
      day: 'Saturday',
      isClosed: false,
      opensAt: '09:00',
      closesAt: '18:00',
    });
    expect(schedule[6].isClosed).toBe(true);
  });

  it('traduce los errores de validación del servidor al campo y al día', () => {
    const problems = mapValidationErrors({
      Name: ['El nombre es obligatorio.'],
      'Schedule[0].ClosesAt': ['La hora de cierre debe ser posterior a la hora de apertura.'],
      Photo: ['Solo se aceptan imágenes JPG, PNG o WebP.'],
    });

    expect(problems.name).toBe('El nombre es obligatorio.');
    expect(problems.photo).toBe('Solo se aceptan imágenes JPG, PNG o WebP.');
    expect(problems.days).toEqual({
      Monday: 'La hora de cierre debe ser posterior a la hora de apertura.',
    });
    expect(problems.schedule).toBe('Revisa los días del horario.');
  });
});
