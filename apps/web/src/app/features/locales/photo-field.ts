import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { HlmButton } from '@mousedent/ui/button';
import { LocalIcon } from './local-icon';

export type PhotoFieldState = 'empty' | 'saved' | 'pending' | 'removed';

/**
 * Área de fotografía del local. Admite un archivo elegido o arrastrado, muestra
 * la vista previa, permite ampliarla, cambiarla o quitarla y deshacer el
 * cambio antes de guardar. No sube nada por su cuenta: el formulario envía la
 * foto junto con los demás datos.
 */
@Component({
  selector: 'app-photo-field',
  imports: [HlmButton, LocalIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './photo-field.html',
})
export class PhotoField {
  readonly previewUrl = input<string | null>(null);
  readonly busy = input(false);
  readonly state = input<PhotoFieldState>('empty');
  readonly fileLabel = input<string | null>(null);
  readonly fileDetail = input<string | null>(null);
  readonly error = input<string | null>(null);
  readonly canUndo = input(false);
  readonly undoLabel = input('Deshacer');

  readonly fileChosen = output<File>();
  readonly remove = output<void>();
  readonly undo = output<void>();
  readonly view = output<void>();

  protected readonly dragging = signal(false);

  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');

  protected openPicker(): void {
    this.picker()?.nativeElement.click();
  }

  protected onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    // Permite elegir el mismo archivo otra vez después de un cambio o un error.
    input.value = '';

    if (file) {
      this.fileChosen.emit(file);
    }
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    const stage = event.currentTarget as HTMLElement | null;

    if (!stage || !stage.contains(event.relatedTarget as Node | null)) {
      this.dragging.set(false);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);

    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.fileChosen.emit(file);
    }
  }
}
