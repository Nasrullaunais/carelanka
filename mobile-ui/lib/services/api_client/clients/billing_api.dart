// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/add_bill_charge_request.dart';
import '../models/bill.dart';
import '../models/billing_rate_book.dart';
import '../models/outstanding_bill_paged_result.dart';
import '../models/settle_bill_request.dart';
import '../models/update_billing_rates_request.dart';

part 'billing_api.g.dart';

@RestApi()
abstract class BillingApi {
  factory BillingApi(Dio dio, {String? baseUrl}) = _BillingApi;

  @GET('/admissions/{admissionId}/bill')
  Future<Bill> getAdmissionBill({
    @Path('admissionId') required String admissionId,
  });

  @POST('/admissions/{admissionId}/bill')
  Future<Bill> prepareAdmissionBill({
    @Path('admissionId') required String admissionId,
  });

  @POST('/admissions/{admissionId}/bill/charges')
  Future<Bill> addBillCharge({
    @Path('admissionId') required String admissionId,
    @Body() AddBillChargeRequest? body,
  });

  @DELETE('/admissions/{admissionId}/bill/charges/{lineId}')
  Future<Bill> removeBillCharge({
    @Path('admissionId') required String admissionId,
    @Path('lineId') required String lineId,
  });

  @POST('/admissions/{admissionId}/bill/settle')
  Future<Bill> settleBill({
    @Path('admissionId') required String admissionId,
    @Body() SettleBillRequest? body,
  });

  @GET('/billing/outstanding')
  Future<OutstandingBillPagedResult> listOutstandingBills({
    @Query('includeSettled') bool? includeSettled = false,
    @Query('page') int? page = 1,
    @Query('pageSize') int? pageSize = 20,
    @Query('search') String? search,
  });

  @GET('/appointments/{appointmentId}/bill')
  Future<Bill> getAppointmentBill({
    @Path('appointmentId') required String appointmentId,
  });

  @POST('/appointments/{appointmentId}/bill')
  Future<Bill> prepareAppointmentBill({
    @Path('appointmentId') required String appointmentId,
  });

  @POST('/appointments/{appointmentId}/bill/charges')
  Future<Bill> addAppointmentBillCharge({
    @Path('appointmentId') required String appointmentId,
    @Body() AddBillChargeRequest? body,
  });

  @DELETE('/appointments/{appointmentId}/bill/charges/{lineId}')
  Future<Bill> removeAppointmentBillCharge({
    @Path('appointmentId') required String appointmentId,
    @Path('lineId') required String lineId,
  });

  @POST('/appointments/{appointmentId}/bill/settle')
  Future<Bill> settleAppointmentBill({
    @Path('appointmentId') required String appointmentId,
    @Body() SettleBillRequest? body,
  });

  @GET('/billing/rates')
  Future<BillingRateBook> getBillingRates();

  @PUT('/billing/rates')
  Future<BillingRateBook> updateBillingRates({
    @Body() UpdateBillingRatesRequest? body,
  });
}
