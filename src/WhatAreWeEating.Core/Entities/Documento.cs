namespace WhatAreWeEating.Core.Entities;

/// <summary>
/// Registro de un documento subido (RF-DOC-01). El contenido vive en el almacenamiento de archivos
/// (un volumen, RF-DOC-05) bajo <see cref="ClaveAlmacenamiento"/>; aquí solo van sus metadatos.
/// </summary>
public class Documento
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
    public Guid PropietarioId { get; set; }

    /// <summary>Nombre del archivo en el almacenamiento (un GUID, nunca el nombre que envió el usuario).</summary>
    public string ClaveAlmacenamiento { get; set; } = string.Empty;

    /// <summary>Eliminación lógica (RF-DOC-04): el registro y el archivo se conservan.</summary>
    public bool Eliminado { get; set; }
    public DateTime? FechaEliminacion { get; set; }
}
