package net.revenj

import net.revenj.patterns.ReportHandler

class ReportClassHandler extends ReportHandler[ReportMe.Result, ReportMe] {
  override def before(report: ReportMe): Unit = {
    ReportClassHandler.calledBefore += 1
  }
  override def after(result: ReportMe.Result): ReportMe.Result = {
    ReportClassHandler.calledAfter += 1
    result
  }
}
object ReportClassHandler {
  var calledBefore = 0
  var calledAfter = 0
}