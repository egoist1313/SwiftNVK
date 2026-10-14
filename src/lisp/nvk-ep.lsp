;;; nvk-ep.lsp — этап EP: продольники (блок или россыпь) из именованных линий
;;; Зависит от: nvk-config, nvk-dll, nvk-input, nvk-cs.

(if (not (boundp '*nvk-ep-ins*)) (setq *nvk-ep-ins* nil))

(defun nvk-stage-ep ( / p r)
  (cond
    ((not (nvk-dll-est-p)) nil)
    (t
     (if (null *nvk-ep-ins*)
       (if *nvk-cs-ins*
         (setq *nvk-ep-ins*
           (list (+ (car *nvk-cs-ins*) 80.0) (cadr *nvk-cs-ins*)))
         (progn
           (princ "\n[НВК] укажи точку вставки продольников")
           (setq p (getpoint "\n[НВК] EP точка: "))
           (if p (setq *nvk-ep-ins* p)))))
     (if (null *nvk-ep-ins*)
       nil
       (progn
         (nvk-layout-send)
         (setq r
           (vl-catch-all-apply 'nvk-ep-draw
             (list (car *nvk-ep-ins*)
                   (cadr *nvk-ep-ins*)
                   *nvk-l-scale*
                   *nvk-h-scale*
                   (if *nvk-block-ep* 1 0))))
         (cond
           ((vl-catch-all-error-p r) nil)
           ((null r) nil)
           (t r)))))))

(princ "\n  nvk-ep.lsp")
(princ)
