package br.com.fiap.sombra.clima.aplicacao;

import org.springframework.stereotype.Service;

/** Escolhe a fonte de uma região: o replay, se ela estiver em demonstração, ou o ao vivo. */
@Service
public class FontesMeteorologicas {

    private final ReplayInmet replay;
    private final FonteAoVivo aoVivo;

    public FontesMeteorologicas(ReplayInmet replay, FonteAoVivo aoVivo) {
        this.replay = replay;
        this.aoVivo = aoVivo;
    }

    public FonteMeteorologica para(String codigoDaRegiao) {
        return replay.ativoPara(codigoDaRegiao) ? replay : aoVivo;
    }
}
