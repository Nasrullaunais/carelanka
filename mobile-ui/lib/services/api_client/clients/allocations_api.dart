// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/allocation_dto.dart';
import '../models/allocation_dto_paged_result.dart';
import '../models/allocation_status.dart';
import '../models/create_allocation_request.dart';
import '../models/end_allocation_request.dart';
import '../models/end_allocation_response.dart';

part 'allocations_api.g.dart';

@RestApi()
abstract class AllocationsApi {
  factory AllocationsApi(Dio dio, {String? baseUrl}) = _AllocationsApi;

  @GET('/allocations')
  Future<AllocationDtoPagedResult> listAllocations({
    @Query('shiftId') String? shiftId,
    @Query('staffMemberId') String? staffMemberId,
    @Query('wardId') String? wardId,
    @Query('status') AllocationStatus? status,
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('page') int? page,
    @Query('pageSize') int? pageSize,
  });

  @POST('/allocations')
  Future<AllocationDto> createAllocation({
    @Body() CreateAllocationRequest? body,
  });

  @POST('/allocations/{id}/end')
  Future<EndAllocationResponse> endAllocation({
    @Path('id') required String id,
    @Body() EndAllocationRequest? body,
  });
}
